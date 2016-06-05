FROM ubuntu:xenial

# Get dependencies
RUN apt-get update && apt-get -y install build-essential cmake libcppunit-dev \
	libavcodec-dev libavfilter-dev libavformat-dev libavresample-dev \
	libavutil-dev git mono-xbuild nuget mono-devel

RUN mkdir /build
WORKDIR /build
