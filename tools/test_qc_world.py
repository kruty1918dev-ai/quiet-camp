"""Delivery transport tests; no Unity, campaign files or network are mutated."""
import contextlib
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest

from qc_world import deliver, git


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


if __name__ == '__main__': unittest.main()
