#!/usr/bin/env python3

# Simple Python example that reads a flow cytometry JSON file, as
# created by cyz2json, and plots particle figures.

# import base64
from PIL import Image
import json

# import math
import sys
import numpy as np

# from io import BytesIO
import os


def image_particle(particle_data):
    pulse_data = particle_data["pulseShapes"]
    max_y = len(pulse_data)
    max_x = 50

    a = np.zeros((max_y, max_x), dtype=np.uint8)

    y = 0
    for pulse_shape in pulse_data:
        x = 0
        print(len(pulse_shape["values"]))

        for value in pulse_shape["values"]:
            if x >= max_x:
                break

            a[y, x] = value
            x = x + 1

        y = y + 1

    return a


def main(filename):
    b = os.path.basename(filename)

    data = json.load(open(filename, encoding="utf-8-sig"))

    for particle in data["particles"]:
        id = particle["particleId"]
        a = image_particle(particle)
        np.save(f"{b}-{id}.npy", a)

        # Normalize to 0-1
        a = (a - np.min(a)) / (np.max(a) - np.min(a))

        # Scale to 0-255
        a = a * 255

        # Convert to integers
        a = a.astype(np.uint8)

        image = Image.fromarray(a)
        image.save(f"{b}-{id}.png")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("image-pulses.py <filename.cyz.json>")
        sys.exit(1)

    main(sys.argv[1])
