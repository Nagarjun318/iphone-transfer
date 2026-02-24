# GitHub Secrets Setup Guide

# This file explains EXACTLY which secrets you need in GitHub
# and how to get each one.
#
# Go to: GitHub repo → Settings → Secrets and variables → Actions → New secret

# ─────────────────────────────────────────────────────────────────────────────
# SECRET 1: VERCEL_TOKEN
# WHY: Authorizes GitHub Actions to deploy to your Vercel account
# ─────────────────────────────────────────────────────────────────────────────
#
# How to get it:
#   1. Go to https://vercel.com/account/tokens
#   2. Click "Create Token"
#   3. Name: "github-actions-iphone-transfer"
#   4. Scope: Full Account (or specific team)
#   5. Expiry: No Expiration (for permanent CI)
#   6. Copy the token value
#
# Secret name : VERCEL_TOKEN
# Secret value: vercel_xxxxxxxxxxxxxxxxxxxxx

# ─────────────────────────────────────────────────────────────────────────────
# SECRET 2: VERCEL_ORG_ID
# WHY: Identifies your Vercel team/organization
# ─────────────────────────────────────────────────────────────────────────────
#
# How to get it:
#   Option A (via CLI):
#     npx vercel whoami
#     # OR
#     cat ~/.local/share/com.vercel.cli/auth.json   # Linux/Mac
#     # Windows: %APPDATA%\Vercel\auth.json
#
#   Option B (via website):
#     1. Go to https://vercel.com/
#     2. Click your profile → Settings
#     3. "General" tab → find "Team ID" or "User ID"
#     4. It starts with "team_" or "user_"
#
# Secret name : VERCEL_ORG_ID
# Secret value: team_xxxxxxxxxxxxxxxxxxxxxxxx

# ─────────────────────────────────────────────────────────────────────────────
# SECRET 3: VERCEL_PROJECT_ID
# WHY: Identifies which Vercel project to deploy to
# ─────────────────────────────────────────────────────────────────────────────
#
# How to get it:
#   Step 1: Create a Vercel project first (one-time setup):
#     cd /path/to/file_transfere_app/vercel-site
#     npx vercel
#     # Follow prompts: link to existing or create new project
#     # Name it: iphone-photo-transfer (or whatever you like)
#
#   Step 2: Get project ID:
#     cat .vercel/project.json
#     # Shows: { "orgId": "...", "projectId": "prj_..." }
#
#   OR via website:
#     1. Go to https://vercel.com/dashboard
#     2. Click your project
#     3. Settings → General → "Project ID"
#
# Secret name : VERCEL_PROJECT_ID
# Secret value: prj_xxxxxxxxxxxxxxxxxxxxxxxx

# ─────────────────────────────────────────────────────────────────────────────
# SECRET 4: VERCEL_PROJECT_URL
# WHY: Used to inject the correct InstallUrl into ClickOnce manifest
#      so auto-update knows where to check for new versions
# ─────────────────────────────────────────────────────────────────────────────
#
# This is your Vercel deployment URL.
# Format: your-project-name.vercel.app (NO https://)
#
# How to find it:
#   1. Go to https://vercel.com/dashboard
#   2. Click your project
#   3. Look at the "Domains" section
#   4. Copy the .vercel.app URL (e.g., iphone-photo-transfer.vercel.app)
#
# Secret name : VERCEL_PROJECT_URL
# Secret value: iphone-photo-transfer.vercel.app

# ─────────────────────────────────────────────────────────────────────────────
# QUICK SETUP CHECKLIST
# ─────────────────────────────────────────────────────────────────────────────
#
# [ ] 1. Install Vercel CLI: npm i -g vercel
# [ ] 2. Login: npx vercel login
# [ ] 3. Create project: cd vercel-site && npx vercel
# [ ] 4. Get project.json: cat .vercel/project.json
# [ ] 5. Go to GitHub repo → Settings → Secrets → Actions
# [ ] 6. Add VERCEL_TOKEN
# [ ] 7. Add VERCEL_ORG_ID
# [ ] 8. Add VERCEL_PROJECT_ID
# [ ] 9. Add VERCEL_PROJECT_URL
# [ ] 10. Push to main branch → watch Actions tab for deployment
# [ ] 11. Visit https://your-project.vercel.app → click download!
