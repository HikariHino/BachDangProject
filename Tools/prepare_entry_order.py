"""Rebuild the natural human trap-entry shout from the cached battle recording."""
import subprocess,sys
from pathlib import Path
root=Path(__file__).resolve().parents[1]
subprocess.run([sys.executable,str(root/'Tools/prepare_battle_recordings.py'),'--entry-only'],cwd=root,check=True)
