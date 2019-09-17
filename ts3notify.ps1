# cut to the first 7 chars of the commit hash
$commitCut = $env:APPVEYOR_REPO_COMMIT.Substring(0, 7)
# and set up a nice teamspeak link for it
$commitLink = "[url=https://github.com/$env:APPVEYOR_PROJECT_SLUG/commit/$env:APPVEYOR_REPO_COMMIT]$commitCut[/url]"
# now we do the same for the appveyor build
$buildLink = "[url=https://ci.appveyor.com/project/$env:APPVEYOR_PROJECT_SLUG/builds/$env:APPVEYOR_BUILD_ID]Build #$env:APPVEYOR_BUILD_NUMBER[/url]"

$gitTitle = git log --format=%B -n 1 HEAD | Out-String

$finalMsg = [System.Uri]::EscapeDataString("Commit $commitLink in branch $env:APPVEYOR_REPO_BRANCH ($buildLink) done.`n Summary: $gitTitle")
$finalMsg = $finalMsg.Replace("(", "%28").Replace(")", "%29")

Invoke-RestMethod -Uri "https://bot.splamy.de/api/bot/template/splamy/(/pm/channel/$finalMsg"
