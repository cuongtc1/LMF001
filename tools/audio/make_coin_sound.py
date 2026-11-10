"""Make the soft coin "ting" (round 72): two short bell notes, no harsh metal (the producer dislikes metal sounds).
Run from the repo root: python tools/audio/make_coin_sound.py   (needs ffmpeg on PATH)"""
import os
import subprocess
import tempfile
import wave

import numpy as np

RATE = 44100
OUT = 'assets/audio/sfx/own/coin.ogg'


def bell(freq, seconds, decay):
    t = np.arange(int(RATE * seconds)) / RATE
    tone = np.sin(2 * np.pi * freq * t) + 0.25 * np.sin(2 * np.pi * freq * 2 * t)
    attack = np.minimum(1, t / 0.004)
    return tone * attack * np.exp(-t * decay)


def main():
    first = bell(988, 0.07, 18)       # B5, cut short
    second = bell(1319, 0.32, 11)     # E6, rings out
    sound = np.concatenate([first, second]) * 0.35
    pcm = (np.clip(sound, -1, 1) * 32767).astype(np.int16)
    with tempfile.TemporaryDirectory() as folder:
        path = os.path.join(folder, 'coin.wav')
        with wave.open(path, 'wb') as file:
            file.setnchannels(1)
            file.setsampwidth(2)
            file.setframerate(RATE)
            file.writeframes(pcm.tobytes())
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', path, '-c:a', 'libvorbis', '-q:a', '5', OUT], check=True)


if __name__ == '__main__':
    main()
