"""문서 갱신이 같은 플레이를 반복하거나 낡은 사진을 재사용하지 않는지 확인해요."""
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import update_docs


class PhotoCacheTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.capture = self.root / "Temp/FlowShots"
        self.capture.mkdir(parents=True)
        self.code = self.root / "Assets/Scripts/Game.cs"
        self.code.parent.mkdir(parents=True)
        self.code.write_text("first")
        (self.capture / "done.txt").write_text("[FlowShots] 사진 1장, 오류 0개", encoding="utf-8")
        (self.capture / "manifest.tsv").write_text("001.png\ttest\tcaption\tline\n", encoding="utf-8")
        (self.capture / "001.png").write_bytes(b"photo")
        self.patches = [patch.object(update_docs, "ROOT", str(self.root)), patch.object(update_docs, "TEMP", str(self.root / "Temp"))]
        for p in self.patches:
            p.start()
        self.addCleanup(self.temp.cleanup)
        self.addCleanup(lambda: [p.stop() for p in self.patches])
        self.remember()

    def remember(self):
        (self.capture / "inputs.json").write_text(json.dumps({"signature": update_docs.screen_signature(),
            "done": (self.capture / "done.txt").stat().st_mtime}), encoding="utf-8")

    def test_same_files_can_reuse_photos(self):
        self.assertTrue(update_docs.shots_fresh())

    def test_changed_content_with_original_timestamp_needs_capture(self):
        old = self.code.stat()
        self.code.write_text("other")
        os.utime(self.code, ns=(old.st_atime_ns, old.st_mtime_ns))
        self.assertFalse(update_docs.shots_fresh())

    def test_deleted_or_renamed_input_needs_capture(self):
        self.code.rename(self.code.with_name("Renamed.cs"))
        self.assertFalse(update_docs.shots_fresh())
        self.code.with_name("Renamed.cs").unlink()
        self.assertFalse(update_docs.shots_fresh())

    def test_missing_or_failed_photo_result_needs_capture(self):
        (self.capture / "001.png").unlink()
        self.assertFalse(update_docs.shots_fresh())
        (self.capture / "001.png").touch()
        (self.capture / "done.txt").write_text("오류 1개", encoding="utf-8")
        self.assertFalse(update_docs.shots_fresh())

    def test_sound_only_change_reuses_photos(self):
        sound = self.root / "Assets/Resources/Sound/new.wav"
        sound.parent.mkdir(parents=True)
        sound.write_bytes(b"new sound")
        self.assertTrue(update_docs.shots_fresh())
        self.assertFalse(str(sound.relative_to(self.root)).replace(os.sep, "/").startswith(update_docs.SCREEN))


if __name__ == "__main__":
    unittest.main()
