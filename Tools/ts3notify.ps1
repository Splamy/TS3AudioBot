Param(
	[bool]$ok
)

# cut to the first 7 chars of the commit hash
$commitCut = $env:APPVEYOR_REPO_COMMIT.Substring(0, 7)
# and set up a nice teamspeak link for it
$commitLink = "[url=https://github.com/$env:APPVEYOR_REPO_NAME/commit/$env:APPVEYOR_REPO_COMMIT]$commitCut[/url]"
# now we do the same for the appveyor build
$buildLink = "[url=https://ci.appveyor.com/project/$env:APPVEYOR_REPO_NAME/builds/$env:APPVEYOR_BUILD_ID]Build #$env:APPVEYOR_BUILD_NUMBER[/url]"

$gitTitle = git log --format=%B -n 1 HEAD | Out-String

$finalMsg = "Commit $commitLink in branch $env:APPVEYOR_REPO_BRANCH ($buildLink) "
if ($ok) {
	$finalMsg = $finalMsg + "[b][color=green]succeeded[/b].`n Summary: $gitTitle"
} else {
	$finalMsg = $finalMsg + "[b][color=red]failed[/b].`n Summary: $gitTitle"
}
$finalMsg = [System.Uri]::EscapeDataString($finalMsg)
$finalMsg = $finalMsg.Replace("(", "%28").Replace(")", "%29")

try { Invoke-RestMethod -Uri "https://bot.splamy.de/api/bot/template/splamy/(/xecute(/pm/channel/$finalMsg)" }
catch {
	Write-Host "Failed to notify:"
	Write-Host $_
}
