#!/usr/bin/env python3
"""Build a reproducible before/after report from the two native audit datasets."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
from analyze_performance import distribution

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'docs/performance/2026-10-09'


def load(directory):
    summary = json.loads((directory / 'summary.json').read_text())
    payload = gzip.decompress((directory / 'editor-audit.json.gz').read_bytes())
    assert hashlib.sha256(payload).hexdigest() == summary['rawJsonSha256']
    return summary, json.loads(payload)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--optimized', type=Path, default=BASE / 'optimized')
    parser.add_argument('--plots', action='store_true')
    args = parser.parse_args()
    before, raw_before = load(BASE)
    after, raw_after = load(args.optimized)
    map_before, _ = load(BASE / 'roadmap')
    map_after, _ = load(args.optimized / 'roadmap')
    metrics = []

    def metric(label, old, new, budget, measure, note=''):
        metrics.append({'label': label, 'beforeMs': old, 'afterMs': new, 'budgetMs': budget,
                        'budgetMet': None if budget is None else new <= budget,
                        'changePercent': (new / old - 1) * 100 if old else None,
                        'measure': measure, 'note': note})

    def method(summary, name, percentile):
        return next(m for m in summary['methods'] if m['name'] == 'QC.' + name)[percentile]

    def stage(summary, name, measure='p95'):
        return next(s for s in summary['scenarios'] if s['stage'] == name)['wallMs'][measure]

    def leaf_work(raw):
        # Include zero-work frames. Per-call p95 after GPU flight describes rare
        # activation/cold builds, not recurring animation cost.
        snapshots = {s['frame'] for s in raw['snapshots']}
        frames = {f['frame']: 0.0 for f in raw['frames'] if f['stage'].startswith('leaves.tier2.normal.')
                  and f['transition'] != 'Idle' and f['frame'] not in snapshots}
        for span in raw['spans']:
            if span['frame'] in frames and span['name'] in ['QC.LeafCurtainGraphic.OnPopulateMesh', 'QC.LeafCurtainGraphic.SetFrame']:
                frames[span['frame']] += span['durationMs']
        return {'frames': len(frames), 'cpuPerFrameMs': distribution(frames.values()),
                'meshCalls': sum(s['name'] == 'QC.LeafCurtainGraphic.OnPopulateMesh' and s['frame'] in frames for s in raw['spans'])}

    leaves = {'before': leaf_work(raw_before), 'after': leaf_work(raw_after)}
    metric('Листя High: щокадрова CPU-робота', leaves['before']['cpuPerFrameMs']['p95'],
           leaves['after']['cpuPerFrameMs']['p95'], 4, 'p95 сумарного leaf CPU work на анімований кадр, включно з нульовими rebuilds',
           'Після оптимізації включено також новий SetFrame scope; до оптимізації виміряно mesh scope. Холодні побудови враховані, але їхній max наведено окремо.')
    for label, name, budget in [('Camp Start', 'CampSceneHost.Start', 100), ('Menu Start', 'MenuSceneHost.Start', 100),
                                ('Atmosphere configure', 'CampAtmosphere.Configure', 100),
                                ('Album/floor updates', 'VisibleForestFloor.LateUpdate', 2)]:
        metric(label, method(before, name, 'p95'), method(after, name, 'p95'), budget, 'p95 inclusive synchronous method, ms')
    metric('Відкриття Levels', stage(before, 'ui.Levels.open', 'max'), stage(after, 'ui.Levels.open', 'max'), 100,
           'max completed-frame wall interval, ms')
    for name, label in [('ui.map.steady','Мапа: нерухома'),('ui.map.sweep','Мапа: повний sweep'),('ui.map.drag','Мапа: плавний drag')]:
        metric(label, stage(map_before, name), stage(map_after, name), 33.33, 'p95 completed-frame wall interval, detailed map run, ms')
    routes = []
    for old, new in zip(before['routeComparisons'], after['routeComparisons']):
        assert old['direction'] == new['direction']
        for mode in ['normal', 'reduced']:
            assert old[mode]['repetitions'] == new[mode]['repetitions'] == 3
            metric('Route '+old['direction']+' / '+mode, max(old[mode]['maxWallMsPerRun']), max(new[mode]['maxWallMsPerRun']), 100,
                   'maximum completed-frame wall interval across three routes, ms', 'До/після на тому самому Editor-стенді; фон меню випадковий, ОС зайнята. Це спостереження, не рандомізований device A/B.')
            routes.append({'direction':old['direction'],'mode':mode,'before':old[mode],'after':new[mode]})
    for scenario in ['camp.summer.tier2.solved','season.spring.high.solved','season.autumn.high.solved','season.winter.high.solved','season.late-haven.high.solved']:
        # Keep exact names from the fixture; optional seasonal labels vary in historical datasets.
        if any(s['stage']==scenario for s in before['scenarios']) and any(s['stage']==scenario for s in after['scenarios']):
            metric(scenario,stage(before,scenario),stage(after,scenario),33.33,'p95 completed-frame wall interval, steady window, ms')
    output = {'schemaVersion':1,'beforeCapturedUtc':before['capturedUtc'],'afterCapturedUtc':after['capturedUtc'],
              'sourceCheckpoint':after['source']['sourceCommit'],'metrics':metrics,'leaves':leaves,'routes':routes,
              'limits':'Same host, viewport and synthetic flow; sequential runs and busy desktop. No player build, target-device FPS or GPU attribution. Changed floor initialization is measured over separate per-tile scopes and total route frames.'}
    args.optimized.joinpath('comparison.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n')
    if args.plots:
        import matplotlib
        matplotlib.use('Agg')
        import matplotlib.pyplot as plt
        plt.rcParams.update({'font.family':'DejaVu Sans','font.size':10,'axes.spines.top':False,'axes.spines.right':False,
                             'figure.facecolor':'#faf9f3','axes.facecolor':'#faf9f3'})
        selected=metrics[:9]
        fig,ax=plt.subplots(figsize=(12,6))
        y=list(range(len(selected)))
        ax.barh([i-.18 for i in y],[m['beforeMs'] for m in selected],.34,color='#ad6746',label='Before')
        ax.barh([i+.18 for i in y],[m['afterMs'] for m in selected],.34,color='#3f6954',label='Optimized')
        ax.set_yticks(y,[m['label'] for m in selected]);ax.invert_yaxis();ax.set_xscale('log')
        ax.set_xlabel('milliseconds · logarithmic scale · each row uses its stated metric')
        ax.set_title('Quiet Camp · native Editor · before / after optimization')
        for i,m in enumerate(selected):ax.text(max(m['beforeMs'],m['afterMs'])*1.06,i,f"{m['beforeMs']:.2f} → {m['afterMs']:.2f}",va='center',fontsize=8)
        ax.set_xlim(.001,max(max(m['beforeMs'],m['afterMs']) for m in selected)*4)
        ax.legend();fig.tight_layout();fig.savefig(args.optimized/'comparison.png',dpi=160);plt.close(fig)
    print(json.dumps({'metrics':len(metrics),'budgetsMet':sum(m['budgetMet'] is True for m in metrics),'budgetMisses':[m['label'] for m in metrics if m['budgetMet'] is False]},ensure_ascii=False))


if __name__ == '__main__':
    main()
