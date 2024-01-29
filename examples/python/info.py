#!/usr/bin/env python3

# Display summary information for a cyz.json file.

import json
import sys


def channels(data):
    return data["instrument"]["channels"]


def num_channels(data):
    return len(channels(data))


def num_particles(data):
    return len(data["particles"])


def main(filename):
    data = json.load(open(filename, encoding="utf-8-sig"))
    print(f"num_channels : {num_channels(data)}")
    print(f"num_particles : {num_particles(data)}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("info.py <filename.cyz.json>")
        sys.exit(1)

    main(sys.argv[1])
