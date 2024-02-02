#!/usr/bin/env python3

# Simple Python example that reads a flow cytometry JSON file, as
# created by cyz2json, and plots particle figures.

# import base64
from PIL import Image
import json
import argparse

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


def main(filename, output_dir):
    b = os.path.basename(filename)

    data = json.load(open(filename, encoding="utf-8-sig"))

    for particle in data["particles"]:
        id = particle["particleId"]
        a = image_particle(particle)
        # np.save(f"{output_dir}/{b}-{id}.npy", a)

        # Normalize to 0-1
        a = (a - np.min(a)) / (np.max(a) - np.min(a))

        a = a * (256 * 256 * 256 - 1)

        red = a // (256**2)
        green = (a // 256) % 256
        blue = a % 256

        rgb_image_array = np.stack((red, green, blue), axis=-1)

        # Convert the 3D array into an image using Pillow
        image = Image.fromarray(np.uint8(rgb_image_array))

        # # Convert to integers
        # a = a.astype(np.uint8)

        # image = Image.fromarray(a)
        image.save(f"{output_dir}/{b}-{id}.png")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Dump listmode data from a CYZ file.")

    parser.add_argument("input_file", type=str, help="The input CYZfile")
    parser.add_argument("--output", type=str, help="The output directorye (optional)")

    args = parser.parse_args()

    main(args.input_file, args.output)
