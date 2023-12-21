#!/usr/bin/env python3

# Simple Python example that reads a flow cytometry JSON file, as
# created by cyz2json, and plots particle figures.

import base64
from PIL import Image
import json
import math
import matplotlib.pyplot as plt
from io import BytesIO


def channels(data):
    return data["instrument"]["channels"]


def num_channels(data):
    return len(channels(data))


def num_particles(data):
    return len(data["particles"])


def find_particle(data, particle_id):
    for particle in data["particles"]:
        if particle["particleId"] == particle_id:
            return particle
    return None


def find_image(data, particle_id):
    for image in data["images"]:
        if image["particleId"] == particle_id:
            return image
    return None


def plot_pulse_shapes(particle_data):
    """Plot particle channels."""
    title = particle_data["particleId"]
    l = len(particle_data["pulseShapes"])
    width = math.floor(math.sqrt(l))
    height = math.ceil(l / width)

    fig, ax = plt.subplots(nrows=height, ncols=width)
    plt.suptitle(f"Particle {title}")

    for i, pulse_shape in enumerate(particle_data["pulseShapes"]):
        row = ax[i // width]
        col = row[i % width]

        values = pulse_shape["values"]
        x = range(len(values))
        title = pulse_shape["description"]
        col.plot(x, values, marker="o", linestyle="-", ms=0.1)
        col.title.set_text(title)

    plt.tight_layout()
    plt.show()


def plot_particle(data, particle_id):
    particle_data = find_particle(data, particle_id)
    title = particle_data["particleId"]
    image_data = find_image(data, particle_id)
    image = load_image(image_data)

    img_height, img_width = image.size
    aspect_ratio = img_width / img_height

    fig, ax = plt.subplots()

    plt.title(f"Particle {title}")

    ax.set_prop_cycle(
        "color",
        ["green", "black", "yellow", "orange", "red", "indigo", "violet", "blue"],
    )

    for i, pulse_shape in enumerate(particle_data["pulseShapes"]):
        y = pulse_shape["values"]
        x = range(len(y))
        ax.plot(x, y, label=pulse_shape["description"])

    ax.legend(loc="upper right")

    ax.set_ylabel("Level [mV]")
    ax.set_xlabel("Sample")

    img_box_height = 0.2  # Height of the image box
    img_box_width = img_box_height * aspect_ratio

    imagebox = plt.axes(
        [0.14, 1 - img_box_height - 0.05, img_box_width, img_box_height], anchor="NW"
    )

    imagebox.imshow(image, cmap="gray")
    imagebox.axis("off")  # Turn off axis

    plt.show()


def load_image(image_data):
    image_data = base64.b64decode(image_data["base64"])

    # Convert bytes data to an image
    image = Image.open(BytesIO(image_data))

    # Return the image object
    return image


def plot_image(image_data):
    """Plot a particle image."""
    id = image_data["particleId"]

    image = load_image(image_data)

    plt.title(f"Particle {id}")
    plt.imshow(image, cmap="gray")
    plt.show()


# Simple example

filename = "../data/sample.cyz.json"

data = json.load(open(filename, encoding="utf-8-sig"))

plot_pulse_shapes(find_particle(data, 1825))

plot_image(find_image(data, 1825))

plot_particle(data, 1825)
