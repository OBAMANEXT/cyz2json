#!/usr/bin/env python3

# A partial implementation of "list mode" for a given JSON CYZ file.
# Outputs a CSV file with summary data for each of the particles.

import json
import statistics
import pandas as pd
import sys
import os


def extract(particles):
    lines = []

    for particle in particles:
        line = {}

        line["id"] = particle["particleId"]

        for pulse_shape in particle["pulseShapes"]:
            values = pulse_shape["values"]
            title = pulse_shape["description"]

            # Ignore these curves
            if title.lower() not in [
                "forward scatter left",
                "forward scatter right",
                "curvature",
            ]:
                title = title.replace(" ", "_").lower()

                line[f"{title}_total"] = sum(values)
                line[f"{title}_maximum"] = max(values)
                line[f"{title}_minimum"] = min(values)
                line[f"{title}_first"] = values[0]
                line[f"{title}_last"] = values[-1]
                line[f"{title}_average"] = statistics.mean(values)

                N = len(values) - 1

                line[f"{title}_centre_of_gravity"] = sum(
                    [n * values[n] for n in range(len(values))]
                ) / sum(values)

                line[f"{title}_fill_factor"] = (sum(values) ** 2) / (
                    (N + 1) * sum(x**2 for x in values)
                )

                # line[f"{title}_length"] = min(values)  # TODO
                # line[f"{title}_number_of_cells"] = min(values)  # TODO

                # line[f"median_{title}"] = statistics.median(values)

        lines.append(line)

    return lines


# filename = "../data/sample.cyz.json"


def main(filename):
    data = json.load(open(filename, encoding="utf-8-sig"))
    lines = extract(data["particles"])
    df = pd.DataFrame(lines)
    outfile = os.path.basename(filename)
    df.to_csv(f"{outfile}.csv", index=False)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("listmode.py <filename.cyz.json>")
        sys.exit(1)

    main(sys.argv[1])
