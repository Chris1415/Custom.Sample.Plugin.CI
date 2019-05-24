param(
	[string]$PathToSolution = "C:\Code\SXC910Playground\Customer.Sample.Solution.sln",
	[string]$PathToPublishOutput = "C:\Code\Output",
	[string]$OutputPackageName = "C:\Code\CommerceLocalDeploymentPackage.zip",
	[string]$Configuration = "debug",
	[string]$PathToDeploy = "C:\inetpub\wwwroot\CommerceAuthoring_Sc9",
	[string]$CommerceSiteName = "CommerceAuthoring_Sc9",
	[string]$BootrapUrl = "https://commerceauthoring.sc9.qa/commerceops/Bootstrap()",
	[string]$UrlIdentityServerGetToken = "https://sc911.identityserver/connect/token",
	[string]$UrlCheckCommandStatus = "https://localhost:5000/commerceops/CheckCommandStatus(taskId=taskIdValue)",
	[string]$HostingEnvironment = "Development",
	[string]$Framework = "net471",
	[string]$Runtime = "win7-x64"
)

$SitecoreAdminAccount = @{
	userName = "sitecore\admin"
	password = "b"
}

#Helper
function Stop-AppPool ($webAppPoolName,[int]$secs) {
$retvalue = $false
$wsec = (get-date).AddSeconds($secs)
Stop-WebAppPool -Name $webAppPoolName
Write-Output "$(Get-Date) waiting up to $secs seconds for the WebAppPool '$webAppPoolName' to stop"
$poolNotStopped = $true
while (((get-date) -lt $wsec) -and $poolNotStopped) {
    $pstate =  Get-WebAppPoolState -Name $webAppPoolName
    if ($pstate.Value -eq "Stopped") {
        Write-Output "$(Get-Date): WebAppPool '$webAppPoolName' is stopped"
        $poolNotStopped = $false
        $retvalue = $true
    }
}
return $retvalue
}

# Import Modules
import-module WebAdministration

# Dotnet Publish
Write-Host "$(Get-Date) Dotnet Publish with parameters `n Solution: $PathToSolution `n OutputPath: $PathToPublishOutput `n Configuration: $Configuration `n Framework: $Framework `n Runtime: $Runtime" -ForegroundColor yellow
dotnet publish $PathToSolution -o $PathToPublishOutput -c $Configuration -f $Framework -r $Runtime
Write-Host "$(Get-Date) Dotnet Publish done" -ForegroundColor Green

# Stop the Website for deployment
Write-Host "$(Get-Date) Stopping Commerce Website $CommerceSiteName" -ForegroundColor yellow
Stop-AppPool $CommerceSiteName 30
# Stop-WebSite -Name $CommerceSiteName
# Stop-WebAppPool -Name $CommerceSiteName
Write-Host "$(Get-Date) Stopped Commerce Website $CommerceSiteName" -ForegroundColor Green

#copied deployment package
Write-Host "$(Get-Date) deploying commerce engine" -ForegroundColor yellow
$joinedPathToPublish = Join-Path $PathToPublishOutput *
Write-Host "$(Get-Date) Copy content from $joinedPathToPublish to $PathToDeploy" -ForegroundColor yellow
Copy-Item -Path $joinedPathToPublish -Destination $PathToDeploy -Recurse -Force
# Expand-Archive -LiteralPath $OutputPackageName -DestinationPath $PathToDeploy -Force
Write-Host "$(Get-Date) deployed commerce engine" -ForegroundColor Green

# Start the Website again after deployment
Write-Host "$(Get-Date) Starting Commerce Website $CommerceSiteName" -ForegroundColor yellow
Start-WebAppPool -Name $CommerceSiteName
Start-WebSite -Name $CommerceSiteName
Write-Host "$(Get-Date) Started Commerce Website $CommerceSiteName" -ForegroundColor Green

# Get Token from Identity Server
Write-Host "$(Get-Date) Get token from identity Server via $UrlIdentityServerGetToken" -ForegroundColor yellow
$headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
$headers.Add("Content-Type", 'application/x-www-form-urlencoded')
$headers.Add("Accept", 'application/json')
$body = @{
	password   = $SitecoreAdminAccount.password
	grant_type = 'password'
	username   = $SitecoreAdminAccount.userName	
	client_id  = 'postman-api'
	scope      = 'openid EngineAPI postman_api'
}
$response = Invoke-RestMethod $UrlIdentityServerGetToken -Method Post -Body $body -Headers $headers
$global:sitecoreIdToken = "Bearer {0}" -f $response.access_token
Write-Host "$(Get-Date) Got token from identity Server $sitecoreIdToken" -ForegroundColor Green

#Call Bootstrap
Write-Host "$(Get-Date) Calling bootstrap via $BootrapUrl" -ForegroundColor yellow
$headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
$headers.Add("Authorization", $global:sitecoreIdToken)
Invoke-RestMethod $BootrapUrl -TimeoutSec 1200 -Method PUT -Headers $headers
Write-Host "$(Get-Date) Called bootstrap" -ForegroundColor Green