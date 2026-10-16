"""Delivery transport tests; no Unity, campaign files or network are mutated."""
import contextlib
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from types import SimpleNamespace
from unittest import mock

from qc_world import deliver, git, main
from render_story_campaign import recipe_for


class DeliveryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='qc-world-cli-test-')
        self.root = Path(self.temporary.name).resolve()
        self.repo = self.root / 'repo'; self.repo.mkdir()
        git(self.repo, 'init', '--initial-branch=main')
        git(self.repo, 'config', 'user.name', 'WorldOps fixture')
        git(self.repo, 'config', 'user.email', 'worldops-fixture@example.invalid')
        (self.repo / 'user.txt').write_text('original', encoding='utf-8')
        (self.repo / 'owned.json').write_text('{}', encoding='utf-8')
        git(self.repo, 'add', '--', 'user.txt', 'owned.json'); git(self.repo, 'commit', '-m', 'Fixture')
        (self.repo / 'owned.json').write_text('{"reviewed":true}', encoding='utf-8')
        (self.repo / 'user.txt').write_text('user work', encoding='utf-8'); git(self.repo, 'add', '--', 'user.txt')

    def tearDown(self):
        assert self.root.name.startswith('qc-world-cli-test-') and self.root.parent == Path(tempfile.gettempdir()).resolve()
        self.temporary.cleanup()

    def proof(self, include_user=False, agent=True):
        files = ['owned.json'] + (['user.txt'] if include_user else [])
        path = self.root / 'delivery.json'
        path.write_text(json.dumps({'repository': str(self.repo), 'agentDelivery': agent,
                                   'preexistingDirtyPaths': ['user.txt'], 'groups': {'level': {
                                       str(self.repo / file): hashlib.sha256((self.repo / file).read_bytes()).hexdigest()
                                       for file in files}}}), encoding='utf-8')
        return path

    def test_delivery_commits_only_owned_files_and_preserves_user_index(self):
        with contextlib.redirect_stdout(io.StringIO()): deliver(self.proof(), 'Reviewed fixture', False)
        self.assertEqual(['owned.json'], git(self.repo, 'diff-tree', '--no-commit-id', '--name-only', '-r', 'HEAD').decode().splitlines())
        self.assertEqual(['user.txt'], git(self.repo, 'diff', '--cached', '--name-only').decode().splitlines())
        self.assertEqual('user work', (self.repo / 'user.txt').read_text(encoding='utf-8'))

    def test_one_protected_member_excludes_the_entire_group(self):
        before = git(self.repo, 'rev-parse', 'HEAD')
        with contextlib.redirect_stdout(io.StringIO()): deliver(self.proof(include_user=True), 'Must not commit', False)
        self.assertEqual(before, git(self.repo, 'rev-parse', 'HEAD'))

    def test_changed_file_or_ui_run_cannot_deliver(self):
        proof = self.proof(); (self.repo / 'owned.json').write_text('later change', encoding='utf-8')
        with self.assertRaises(SystemExit): deliver(proof, 'Must not commit', False)
        with self.assertRaises(SystemExit): deliver(self.proof(agent=False), 'Must not commit', False)


class LegacyRoutingTests(unittest.TestCase):
    def args(self, **changes):
        return SimpleNamespace(**dict(dict(start=9,end=17,compose=False,recipe=None,round=None),**changes))

    def test_capture_only_route_never_repairs_or_publishes(self):
        recipe=recipe_for(self.args())
        self.assertEqual(list(range(9,18)),recipe['orders'])
        self.assertEqual(['audit'],[o['kind'] for o in recipe['operations']])
        self.assertFalse(recipe['autoApply']);self.assertFalse(recipe['criteria']['autoRepair'])

    def test_composition_cannot_fall_back_to_legacy_writer(self):
        with self.assertRaises(ValueError):recipe_for(self.args(compose=True))

    def test_range_cannot_expand_recipe_or_stable_id_selection(self):
        with tempfile.TemporaryDirectory(prefix='qc-world-routing-') as temporary:
            p=Path(temporary)/'recipe.json';p.write_text(json.dumps({'orders':[9,75]}),encoding='utf-8')
            self.assertEqual([9],recipe_for(self.args(recipe=p))['orders'])
            p.write_text(json.dumps({'levelIds':['QC009']}),encoding='utf-8')
            with self.assertRaises(ValueError):recipe_for(self.args(recipe=p))

    def test_transport_retries_a_temporarily_locked_editor_reply(self):
        with tempfile.TemporaryDirectory(prefix='qc-world-reply-') as temporary:
            project=Path(temporary).resolve();folder=project/'Temp/ai';folder.mkdir(parents=True)
            (project/'Temp/UnityLockfile').touch()
            reply=folder/'qc-world-result.json';reply.write_text(json.dumps({'requestId':'fixture','status':'ok'}),encoding='utf-8')
            original=Path.read_text;attempts=[]
            def read(path,*args,**kwargs):
                if path==reply:
                    attempts.append(True)
                    if len(attempts)==1:raise PermissionError('Editor is atomically replacing its reply')
                return original(path,*args,**kwargs)
            with mock.patch('sys.argv',['qc_world.py','--project',str(project),'--timeout','1','status']),mock.patch('qc_world.uuid.uuid4',return_value=SimpleNamespace(hex='fixture')),mock.patch.object(Path,'read_text',read),mock.patch('qc_world.time.sleep'),contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0,main())
            self.assertEqual(2,len(attempts))


if __name__ == '__main__': unittest.main()
