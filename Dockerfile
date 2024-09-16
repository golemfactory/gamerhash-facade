FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build

# Install python, because Golem.Tools require it
RUN apt-get update && apt-get install -y python3 python3-venv python3-pip
RUN ln -s /usr/bin/python3 /usr/bin/python
RUN pip3 install pyinstaller

RUN git clone https://github.com/golemfactory/gamerhash-facade.git
WORKDIR /gamerhash-facade
RUN git checkout headless-facade

# Build necessary application
RUN dotnet build FacadeHeadlessApp
RUN dotnet build Golem.Package

RUN dotnet publish FacadeHeadlessApp --no-restore -o /apps
RUN dotnet publish Golem.Package --no-restore -o /apps


FROM mcr.microsoft.com/dotnet/runtime:7.0-jammy

WORKDIR /apps
COPY --from=build /apps .

RUN ./Golem.Package download --target modules --version v5.1.0
ENTRYPOINT ./FacadeHeadlessApp --golem modules --wallet 0x82a630d2447ffd282657978f9f76c02da8be9819
