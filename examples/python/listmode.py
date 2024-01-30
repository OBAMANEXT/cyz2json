#!/usr/bin/env python3

# A partial implementation of "list mode" for a given JSON CYZ file.
# Outputs a CSV file with summary data for each of the particles.

import json
import statistics
import pandas as pd
import sys
import os
import math


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
                "fws r",
                "fws l",
            ]:
                title = title.replace(" ", "_").lower()

                # print(f"title = {title}")
                # print(f"values = {values}")

                total = sum(values)
                line[f"{title}_total"] = total
                line[f"{title}_maximum"] = max(values)
                line[f"{title}_minimum"] = min(values)
                line[f"{title}_first"] = values[0]
                line[f"{title}_last"] = values[-1]
                line[f"{title}_average"] = statistics.mean(values)

                N = len(values) - 1

                cg = sum([n * values[n] for n in range(len(values))]) / sum(values)

                line[f"{title}_centre_of_gravity"] = cg

                line[f"{title}_fill_factor"] = (sum(values) ** 2) / (
                    (N + 1) * sum(x**2 for x in values)
                )

                corespeed = 2
                beamwidth = 5
                dx = corespeed / 4
                threshold = max(values) / 2

                highs = [x for (x, y) in enumerate(values) if y > threshold]
                first = highs[0]
                last = highs[-1]
                l = last - first + 1
                lraw = l * dx
                u = (beamwidth / lraw) ** 2
                line[f"{title}_length"] = lraw * (
                    70 / (70 + u + 0.5 * u**2 + 1.5 * u**4)
                )

                t1 = sum((values[i] - values[i - 1]) ** 2 for i in range(1, N))
                # print(f"t1 = {t1}")

                t2 = sum(x**2 for x in values) - (total**2 / (N + 1))
                # print(f"t2 = {t2}")

                try:
                    line[f"{title}_number_of_cells"] = (
                        (N + 1) / (2 * math.pi)
                    ) * math.sqrt(t1 / t2)
                except:
                    line[f"{title}_number_of_cells"] = 0

                line[f"{title}_asymmetry"] = abs((2 * cg / (N + 1)) - 1)

                line[f"{title}_inertia"] = (
                    sum([x**2 * y for (x, y) in enumerate(values)]) - cg**2 * total
                ) / (1 / 12 * (N + 1) ** 2 * total)

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
