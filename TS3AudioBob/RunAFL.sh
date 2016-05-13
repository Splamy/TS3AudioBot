#!/usr/bin/env bash
folder=bin

export AFL_SKIP_CPUFREQ=1

afl-fuzz -i testcases -o findings -m 500M -M fuzzer00 -- "$folder/ts3audiobobfuzz" &
for i in `seq \`nproc\``; do
	afl-fuzz -i testcases -o findings -m 500M -S fuzzer0$i -- "$folder/ts3audiobobfuzz" > /dev/null &
done

echo 'Call `afl-whatsup findings` to see the current status'
