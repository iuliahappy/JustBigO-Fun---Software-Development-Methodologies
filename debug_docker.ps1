# Manual Docker Reproduction Script
$workDir = Join-Path $env:TEMP "justbigo_debug"
if (Test-Path $workDir) { Remove-Item -Recurse -Force $workDir }
New-Item -ItemType Directory -Path $workDir

# 1. Create Solution
$solutionCode = @"
def two_sum(nums, target):
    n = len(nums)
    for i in range(n - 1):
        for j in range(i + 1, n):
            if nums[i] + nums[j] == target:
                return [i, j]
    return []
"@
$solutionCode | Out-File -FilePath (Join-Path $workDir "solution.py") -Encoding utf8

# 2. Create Input
$inputJson = '{"nums":[2,7,11,15],"target":9}'
$inputJson | Out-File -FilePath (Join-Path $workDir "input.json") -Encoding utf8

# 3. Create Driver
$driverCode = @"
import json
import sys
import os
sys.path.append('/app')
try:
    import solution
    with open('/app/input.json', 'r') as f:
        data = json.load(f)
    func = getattr(solution, 'two_sum')
    res = func(**data)
    print(json.dumps(res))
except Exception as e:
    import traceback
    traceback.print_exc(file=sys.stderr)
    sys.exit(1)
"@
$driverCode | Out-File -FilePath (Join-Path $workDir "driver.py") -Encoding utf8

# 4. Run Docker
$dockerWorkDir = $workDir.Replace("\", "/")
Write-Host "Running Docker from: $dockerWorkDir"
docker run --rm -v "${dockerWorkDir}:/app" python:3.10-slim sh -c "cd /app && python /app/driver.py"

# 5. Cleanup hint
Write-Host "`nIf this failed with a 'Permission Denied' or 'Mount' error, check Docker Desktop File Sharing settings."
