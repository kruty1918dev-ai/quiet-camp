"""Focused checks for edit validation; synthetic fixtures are never trailer footage."""
import json
from pathlib import Path
import tempfile
import unittest
import cv2
import numpy as np
from review_teaser_v03 import analyze
from package_teaser import takes


class TrailerQaTests(unittest.TestCase):
    def movie(self,root,colors):
        path=root/'synthetic.avi';out=cv2.VideoWriter(str(path),cv2.VideoWriter_fourcc(*'MJPG'),30,(160,90))
        self.assertTrue(out.isOpened())
        for color,frames in colors:
            for _ in range(frames):out.write(np.full((90,160,3),color,dtype=np.uint8))
        out.release();return path

    def test_cuts_duplicates_similar_and_expected_black_tail(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);path=self.movie(root,[((0,0,200),30),((0,180,0),30),((0,0,0),30)])
            result=analyze(path,root/'review',[30,60],2)
            self.assertEqual(result['blackFrames'],list(range(60,90)))
            self.assertEqual(result['blackStartOffsetFrames'],0)
            self.assertTrue(any(abs(n-30)<=1 for n in result['detectedCuts']))
            self.assertTrue(result['duplicateFrameRunCandidates'])
            self.assertTrue(result['similarShotCandidates']) # Flat dHash is ambiguous by design.

    def test_unplanned_black_is_a_failure(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);path=self.movie(root,[((0,0,0),30)])
            with self.assertRaisesRegex(RuntimeError,'Unexpected black'):
                analyze(path,root/'review',[],1)

    def test_stale_receipt_cannot_be_packaged(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);folder=root/'UserSettings/Teaser';folder.mkdir(parents=True)
            movie=root/'gameplay.mp4';movie.write_bytes(b'not-a-real-video')
            receipt=dict(video=str(movie),shotId='one',frames=180,sourceHash='old',recorded=True,memoryOnly=True,renderedFrames=180)
            (root/'capture.json').write_text(json.dumps(receipt))
            job=dict(status='captured',record=True,onlyShot=-1,shotCount=1,totalFrames=180,sourceHash='new',receipts=[receipt])
            (folder/'job-v03-a.json').write_text(json.dumps(job))
            with self.assertRaisesRegex(RuntimeError,'Capture evidence'):
                takes(root,'v03-a')


if __name__=='__main__':unittest.main()
