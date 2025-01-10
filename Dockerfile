# Stage 1: Build the application
FROM ubuntu:20.04 AS build
ENV DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y wget apt-transport-https curl gnupg2
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
ENV PATH="${PATH}:/root/.dotnet"
RUN apt-get install -y libopencv-dev libopencv-contrib-dev # If these are build dependencies
WORKDIR /src
# (Build the app locally)
# COPY . . # REMOVE THIS LINE
# RUN dotnet publish -c Release -o /app/publish # REMOVE THIS LINE
# (After you build the project locally, uncomment and change the following line:)
# COPY bin/Release/net8.0/linux-x64/publish /app/publish

# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
RUN apt-get update && apt-get install -y libopencv-dev libopencv-contrib-dev # If these are runtime dependencies
WORKDIR /app
COPY --from=build /app/publish/ .
ENTRYPOINT ["./Cyz2Json"]
