# Publish ClickOnce to Vercel (local manual publish)
# WHY: Use this script if you want to publish manually without GitHub Actions

param(
    [string]$Version = "1.0.0",
    [string]$VercelToken = $env:VERCEL_TOKEN,
    [string]$InstallUrl = "https://your-app.vercel.app"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$ProjectFile = "$ProjectRoot\src\iPhoneTransfer.UI\iPhoneTransfer.UI.csproj"
$OutputDir = "$ScriptDir\clickonce-output"
$SiteDir = "$ScriptDir"

Write-Host "=== iPhone Transfer — ClickOnce Publisher ===" -ForegroundColor Cyan
Write-Host "Version   : $Version"
Write-Host "InstallUrl: $InstallUrl"
Write-Host "Output    : $OutputDir"
Write-Host ""

# ── Step 1: Clean previous output ─────────────────────────────────────────────
Write-Host "[1/5] Cleaning previous output..." -ForegroundColor Yellow
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# ── Step 2: Build ClickOnce ───────────────────────────────────────────────────
Write-Host "[2/5] Publishing ClickOnce..." -ForegroundColor Yellow

# WHY: msbuild /t:Publish creates:
#   setup.exe                   ← bootstrapper (what users download)
#   iPhoneTransfer.application  ← ClickOnce manifest
#   Application Files/          ← versioned app files
msbuild $ProjectFile `
    /t:Publish `
    /p:Configuration=Release `
    /p:Platform=x64 `
    /p:PublishDir="$OutputDir\" `
    /p:InstallUrl="$InstallUrl/" `
    /p:ApplicationVersion="$Version.0" `
    /p:IsWebBootstrapper=true `
    /p:UpdateEnabled=true `
    /p:UpdateMode=Foreground `
    /p:UpdateInterval=7 `
    /p:UpdateIntervalUnits=Days `
    /p:BootstrapperEnabled=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "msbuild failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Host "   ClickOnce published successfully" -ForegroundColor Green

# ── Step 3: Copy landing page ─────────────────────────────────────────────────
Write-Host "[3/5] Injecting landing page..." -ForegroundColor Yellow

$buildDate = Get-Date -Format "MMMM d, yyyy"

# Copy and inject version/date into index.html
$html = Get-Content "$SiteDir\index.html" -Raw -Encoding UTF8
$html = $html -replace '__VERSION__', $Version
$html = $html -replace '__BUILD_DATE__', $buildDate
Set-Content "$OutputDir\index.html" $html -Encoding UTF8

# Copy vercel.json (routing rules for ClickOnce MIME types)
Copy-Item "$SiteDir\vercel.json" "$OutputDir\vercel.json" -Force

Write-Host "   Landing page copied and version injected" -ForegroundColor Green

# ── Step 4: Verify output ─────────────────────────────────────────────────────
Write-Host "[4/5] Verifying output..." -ForegroundColor Yellow

$required = @("setup.exe", "iPhoneTransfer.application", "index.html", "vercel.json")
foreach ($file in $required) {
    if (Test-Path "$OutputDir\$file") {
        $size = [Math]::Round((Get-Item "$OutputDir\$file").Length / 1MB, 1)
        Write-Host "   ✅ $file ($size MB)" -ForegroundColor Green
    } else {
        Write-Error "   ❌ MISSING: $file"
    }
}

# ── Step 5: Deploy to Vercel ──────────────────────────────────────────────────
Write-Host "[5/5] Deploying to Vercel..." -ForegroundColor Yellow

if (-not $VercelToken) {
    Write-Host ""
    Write-Host "   ⚠️  No VERCEL_TOKEN provided." -ForegroundColor Orange
    Write-Host "   To deploy manually, run:" -ForegroundColor Orange
    Write-Host ""
    Write-Host "   cd $OutputDir" -ForegroundColor White
    Write-Host "   npx vercel --prod --token YOUR_TOKEN" -ForegroundColor White
    Write-Host ""
    Write-Host "   Output is ready in: $OutputDir" -ForegroundColor Cyan
    exit 0
}

# Check if vercel CLI is installed
if (-not (Get-Command vercel -ErrorAction SilentlyContinue)) {
    Write-Host "   Installing Vercel CLI..." -ForegroundColor Yellow
    npm install -g vercel
}

Push-Location $OutputDir
try {
    vercel --prod --token $VercelToken --yes
    if ($LASTEXITCODE -ne 0) { throw "Vercel deploy failed" }
    Write-Host "   ✅ Deployed to Vercel successfully!" -ForegroundColor Green
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== Done! ===" -ForegroundColor Cyan
Write-Host "Your app is live at: $InstallUrl" -ForegroundColor Green
Write-Host "Direct download   : $InstallUrl/setup.exe" -ForegroundColor Green
Write-Host ""
