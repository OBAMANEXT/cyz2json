FROM ubuntu:20.04
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y wget apt-transport-https curl
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0
ENV PATH="${PATH}:/root/.dotnet"
RUN apt-get install -y libopencv-dev libopencv-contrib-dev
WORKDIR /src