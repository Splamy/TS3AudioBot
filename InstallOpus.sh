#!/bin/bash

baseDir=`pwd`

OpusBaseName="opus-1.1.3"
OpusFileName="$OpusBaseName.tar.gz"

# Download the Opus library
if [ ! -e "$OpusFileName" ]; then
    opusLink="http://downloads.xiph.org/releases/opus/$OpusFileName"
    echo "Downloading $opusLink"
    wget "$opusLink"
else
    echo "Opus archive existing already"
fi

# Extract the archive
tar -vxf "$OpusFileName"

# Go into the extracted directory
cd "$OpusBaseName"

# Build the library
./configure && make

# Go back
cd "$baseDir"

# Copy the required libopus.so to the local folder
cp "$OpusBaseName/.libs/libopus.so" "./"

# Copy the libopus.so to the folder we need
cp "./libopus.so" "$baseDir/TS3AudioBot/bin/Release/libopus.so"

echo "Done"