"""Concurrent callers must not overwrite an asynchronous Unity request."""
import subprocess,sys,tempfile,time,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]

class SerialNativeRequests(unittest.TestCase):
    def test_second_client_cannot_replace_an_active_request(self):
        with tempfile.TemporaryDirectory() as folder:
            project=Path(folder);command=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(project),'--mode','authoring-status','--timeout','2']
            first=subprocess.Popen(command,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8')
            try:
                pending=project/'Temp/ai/qc007-art-request.json';deadline=time.monotonic()+1.5
                while not pending.exists() and first.poll() is None and time.monotonic()<deadline:time.sleep(.02)
                self.assertTrue(pending.exists());before=pending.read_bytes()
                second=subprocess.run(command,capture_output=True,text=True,encoding='utf-8',timeout=2)
                self.assertNotEqual(0,second.returncode);self.assertIn('Another native request is running',second.stdout+second.stderr)
                self.assertEqual(before,pending.read_bytes())
            finally:first.communicate(timeout=4)

if __name__=='__main__':unittest.main()
