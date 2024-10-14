# syntax=docker.io/docker/dockerfile:1.7-labs
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build

# Install python, because Golem.Tools need it to build App and it is dependency
# of `FacadeHeadlessApp` so we have no choice.
RUN apt-get update && apt-get install -y python3 python3-venv python3-pip
RUN ln -s /usr/bin/python3 /usr/bin/python
RUN pip3 install pyinstaller

# Or clone from github if using in a place where the code is not available
# RUN git clone https://github.com/golemfactory/gamerhash-facade.git
COPY --exclude=./ext . /gamerhash-facade
WORKDIR /gamerhash-facade

# Build necessary application
RUN dotnet build FacadeHeadlessApp
RUN dotnet build Golem.Package

RUN dotnet publish FacadeHeadlessApp --no-restore -o /apps
RUN dotnet publish Golem.Package --no-restore -o /apps


FROM mcr.microsoft.com/dotnet/runtime:7.0-jammy

WORKDIR /apps
COPY --from=build /apps .

ARG VERSION=v6.1.0
RUN ./Golem.Package download --target modules --version ${VERSION}

COPY ./dummy-offer-overrides.json /dummy-offer-overrides.json
ENV OFFER_OVERRIDE_FILE_PATH="/dummy-offer-overrides.json"

# Uncomment to test locally built binaries
# COPY ./ext/ya-runtime-ai /apps/modules/plugins/ya-runtime-ai
# COPY ./ext/yagna /apps/modules/golem/yagna
# COPY ./ext/dummy /apps/modules/plugins/dummy

ENTRYPOINT ["./FacadeHeadlessApp", "--golem", "modules"]
CMD ["--wallet", "0x82a630d2447ffd282657978f9f76c02da8be9819"]
