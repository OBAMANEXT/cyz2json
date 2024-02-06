#!/usr/bin/env python3

# A partial implementation of "list mode" for a given JSON CYZ file.
# Outputs a CSV file with summary data for each of the particles.

import json
import statistics
import pandas as pd
import sys
import os
import math
import argparse


def extract(particles):
    lines = []

    for particle in particles:
        line = {}

        line["id"] = particle["particleId"]

        for parameter in particle["parameters"]:
            description = parameter["description"]
            line[f"{description}_length"] = parameter["length"]
            line[f"{description}_total"] = parameter["total"]
            line[f"{description}_maximum"] = parameter["maximum"]
            line[f"{description}_average"] = parameter["average"]
            line[f"{description}_inertia"] = parameter["inertia"]
            line[f"{description}_centreOfGravity"] = parameter["centreOfGravity"]
            line[f"{description}_fillFactor"] = parameter["fillFactor"]
            line[f"{description}_asymmetry"] = parameter["asymmetry"]
            line[f"{description}_numberOfCells"] = parameter["numberOfCells"]
            line[f"{description}_sampleLength"] = parameter["sampleLength"]
            line[f"{description}_timeOfArrival"] = parameter["timeOfArrival"]
            line[f"{description}_first"] = parameter["first"]
            line[f"{description}_last"] = parameter["last"]
            line[f"{description}_minimum"] = parameter["minimum"]
            line[f"{description}_swscov"] = parameter["swscov"]
            line[f"{description}_variableLength"] = parameter["variableLength"]

        lines.append(line)

    return lines


# filename = "../data/sample.cyz.json"


def main(filename, outfile):
    data = json.load(open(filename, encoding="utf-8-sig"))
    lines = extract(data["particles"])
    df = pd.DataFrame(lines)
    df.to_csv(outfile, index=False)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Dump listmode data from a CYZ file.")

    parser.add_argument("input_file", type=str, help="The input CYZfile")
    parser.add_argument("--output", type=str, help="The output file name (optional)")

    args = parser.parse_args()

    main(args.input_file, args.output)
