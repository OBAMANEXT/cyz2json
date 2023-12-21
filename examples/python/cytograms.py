#!/usr/bin/env python3

# Simple Python example that reads a flow cytometry JSON file, as
# created by cyz2json, and plots some cytograms.

import json
import matplotlib.pyplot as plt
import pandas as pd
import statistics


def extract(particles):
    df = pd.DataFrame()

    for particle in particles:
        line = {}

        line["id"] = particle["particleId"]

        for pulse_shape in particle["pulseShapes"]:
            values = pulse_shape["values"]
            title = pulse_shape["description"]
            title = title.replace(" ", "_").lower()

            line[f"sum_{title}"] = sum(values)
            line[f"mean_{title}"] = statistics.mean(values)
            line[f"median_{title}"] = statistics.median(values)

        df = pd.concat([df, pd.DataFrame([line])], ignore_index=True)

    return df


def scatter(df, x, y):
    # Creating the scatter plot
    plt.figure(figsize=(8, 6))
    plt.scatter(df[x], df[y], s=0.1)
    plt.xscale("log")
    plt.yscale("log")
    plt.xlabel(f"{x}")
    plt.ylabel(f"{y}")
    plt.title(f"Scatter Plot of {x} and {y}")
    plt.show()


filename = "../data/sample.cyz.json"

data = json.load(open(filename, encoding="utf-8-sig"))

df = extract(data["particles"])

# Check alignment
scatter(df, "sum_forward_scatter_left", "sum_forward_scatter_right")

scatter(df, "mean_fl_red", "mean_fl_yellow")

scatter(df, "sum_fws", "sum_sidewards_scatter")
