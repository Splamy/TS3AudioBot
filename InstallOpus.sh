#!/bin/bash

baseDir=`pwd`

OpusBaseName="opus-1.3.1"
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
./configure && make && sudo make install

# Move to global folder
if [ ! -f /usr/lib/libopus.so ]; then
    sudo cp ".libs/libopus.so" "/usr/lib/"
else
    echo "'/urs/lib/libopus.so' already exists, will not be overwritten"
fi

echo "Done"
