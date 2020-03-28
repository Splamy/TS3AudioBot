#!/bin/bash

function EscTS() {
  escTS=$1
  escTS=${escTS//$'\\'/$'\\\\'}
  escTS=${escTS//$'/'/$'\\/'}
  escTS=${escTS//$' '/$'\\s'}
  escTS=${escTS//$'|'/$'\\p'}
  escTS=${escTS//$'\f'/$'\\f'}
  escTS=${escTS//$'\n'/$'\\n'}
  escTS=${escTS//$'\r'/$'\\r'}
  escTS=${escTS//$'\t'/$'\\t'}
  escTS=${escTS//$'\v'/$'\\v'}
}

if [ -z "$TRAVIS_TEST_RESULT" ]; then
  EscTS "I like to execute scripts :)"
  nc -q 2 splamy.de 10011 <<EOF
use 1
clientupdate client_nickname=Scriptie
sendtextmessage targetmode=3 target=1 msg=$escTS
quit
EOF
  trap '' INT
  echo -e "Ok, ok, so you like to execute random scripts you don't even know what they are doing."
  sleep 4
  echo -e "Like I don't even mention this file \033[5;7manywhere\033[0m in the entire setup tutorial."
  sleep 4
  echo -e "What makes you think this is a good idea???"
  sleep 4
  echo -e "As a punishment you have to watch the entire Star Wars IV movie in ASCII!"
  sleep 4
  telnet "towel.blinkenlights.nl"
  ex=$?
  if [ $ex -eq 0 ]; then
    echo -e "Hope you learned your lesson :P"
  else
	echo -e "...lucky you, seems like you don't have telnet. Ugh, have a nice day anyway."
  fi
  sleep 4
  exit
fi

# check the the build result
if [ $TRAVIS_TEST_RESULT -eq 0 ]; then
  ok="succeeded"
else
  ok="failed"
fi

# cut to the first 7 chars of the commit hash
commitCut=${TRAVIS_COMMIT:0:7}
# and set up a nice teamspeak link for it
commitLink="[url=https://github.com/$TRAVIS_REPO_SLUG/commit/$TRAVIS_COMMIT]$commitCut[/url]"

# now we do the same for the travis build
buildLink="[url=https://travis-ci.org/$TRAVIS_REPO_SLUG/builds/$TRAVIS_BUILD_ID]Build #$TRAVIS_BUILD_NUMBER[/url]"

# get git info
gitTitle=`git log --format=%B -n 1 HEAD`

# form a nice message
lb=$'\n'
EscTS "Commit $commitLink in branch $TRAVIS_BRANCH ($buildLink) $ok.$lb Summary: $gitTitle"

nc -q 2 splamy.de 10011 <<EOF
use sid=1 client_nickname=TravisCI
sendtextmessage targetmode=3 target=1 msg=$escTS
quit
EOF
