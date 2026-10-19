<#
 Fills an EMPTY (just reset) demo database with sample data through the real API, so every rule and
 validation runs exactly as it does for a user. Run order:
   1. Back up, then run demo-reset.sql with @Commit = 1.
   2. Start the API (https://localhost:7100).
   3. powershell -ExecutionPolicy Bypass -File demo-seed.ps1 -AdminUser admin
      (it asks for the Admin password; nothing is stored).

 Creates: LKG, UKG and Class 1-5 (sections A and B, 30 seats each), 3 subjects per section, 2 teachers + a clerk and a supervisor, 6 parents, 7 applications
 (3 enrolled students from two families, 1 sibling, 1 admitted, 1 registered, 1 rejected).
 Login for every created staff user: the -StaffPassword value (default Demo#2026pw).
 It stops at the first error and prints the server's message. Run demo-reset.sql again to start over.
#>
param(
    [string]$BaseUrl = "https://localhost:7100",
    [Parameter(Mandatory = $true)][string]$AdminUser,
    [string]$StaffPassword = "Demo#2026pw"
)

$ErrorActionPreference = "Stop"

# The dev API uses a self-signed certificate.
if ($PSVersionTable.PSVersion.Major -lt 6) {
    Add-Type @"
using System.Net; using System.Security.Cryptography.X509Certificates;
public class TrustAll : ICertificatePolicy {
  public bool CheckValidationResult(ServicePoint s, X509Certificate c, WebRequest r, int p) { return true; } }
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAll
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
}

$script:Token = $null

function Invoke-Api([string]$Method, [string]$Path, $Body = $null) {
    $req = @{
        Uri         = "$BaseUrl$Path"
        Method      = $Method
        ContentType = "application/json"
        Headers     = @{}
    }
    if ($script:Token) { $req.Headers["Authorization"] = "Bearer $script:Token" }
    if ($null -ne $Body) { $req.Body = ($Body | ConvertTo-Json -Depth 8) }
    if ($PSVersionTable.PSVersion.Major -ge 6) { $req.SkipCertificateCheck = $true }
    try {
        return Invoke-RestMethod @req
    }
    catch {
        $detail = $_.ErrorDetails.Message
        if (-not $detail -and $_.Exception.Response) {
            try { $detail = (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { }
        }
        throw "$Method $Path failed: $detail"
    }
}

function Step([string]$Text) { Write-Host "  $Text" -ForegroundColor Cyan }

# ---- sign in -------------------------------------------------------------------------------------
$secure = Read-Host "Password for $AdminUser" -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
$login = Invoke-Api POST "/api/auth/login" @{ usernameOrEmail = $AdminUser; password = $plain }
$script:Token = $login.token
if ($login.mustChangePassword) { throw "This account must change its password first. Sign in on the website once, then run this again." }
Step "Signed in as $($login.username)"

if ((Invoke-Api GET "/api/class-sections").Count -gt 0) {
    throw "The database already has classes. Run demo-reset.sql first (this script is for an empty school)."
}

# ---- classes and subjects ------------------------------------------------------------------------
$classes = @{}
# LKG, UKG and Class 1 to 5, two sections each (A and B), 30 seats per section.
$classNames = @(
    @{ id = "LKG"; name = "LKG"; grade = $null; stage = "Pre-Primary" },
    @{ id = "UKG"; name = "UKG"; grade = $null; stage = "Pre-Primary" },
    @{ id = "1"; name = "Class 1"; grade = 1; stage = "Primary" },
    @{ id = "2"; name = "Class 2"; grade = 2; stage = "Primary" },
    @{ id = "3"; name = "Class 3"; grade = 3; stage = "Primary" },
    @{ id = "4"; name = "Class 4"; grade = 4; stage = "Primary" },
    @{ id = "5"; name = "Class 5"; grade = 5; stage = "Primary" })
foreach ($c in $classNames) {
    foreach ($section in @("A", "B")) {
        $created = Invoke-Api POST "/api/class-sections" @{
            name = $c.name; section = $section; grade = $c.grade; stage = $c.stage
            medium = "English"; stream = "General"; capacity = 30; isActive = $true
        }
        $classes["$($c.id)$section"] = $created.id
    }
}
Step "Created $($classes.Count) class sections (LKG, UKG, Class 1 to 5; sections A and B; 30 seats each)"

$subjects = @{}
foreach ($key in $classes.Keys) {
    foreach ($s in @(@("English", "ENG"), @("Mathematics", "MAT"), @("Science", "SCI"))) {
        $created = Invoke-Api POST "/api/subjects" @{
            name = $s[0]; code = "$($s[1])-$key"; classSectionId = $classes[$key]; type = "Theory"
            maxMarks = 100; passMarks = 35; isActive = $true
        }
        $subjects["$key|$($s[0])"] = $created.id
    }
}
Step "Created $($subjects.Count) subjects"

# ---- staff ---------------------------------------------------------------------------------------
function New-Staff($Username, $Role, $FullName, $Employee) {
    Invoke-Api POST "/api/users" @{
        username = $Username; email = "$Username@demo-school.example"; password = $StaffPassword; roleName = $Role
        profile = @{ fullName = $FullName; gender = "Female"; mobile = "9876509$Employee"; joiningDate = (Get-Date).AddMonths(-6).ToString("yyyy-MM-dd") }
    } | Out-Null
}
New-Staff "teacher.priya" "Teacher" "Priya Nair" "001"
New-Staff "teacher.arjun" "Teacher" "Arjun Menon" "002"
New-Staff "demo.clerk" "Clerk" "Office Clerk" "003"
New-Staff "demo.supervisor" "Supervisor" "Academic Supervisor" "004"
$teachers = Invoke-Api GET "/api/teachers"
$priya = ($teachers | Where-Object { $_.username -eq "teacher.priya" }).id
$arjun = ($teachers | Where-Object { $_.username -eq "teacher.arjun" }).id
Invoke-Api POST "/api/teachers/$priya/subjects" @{
    classSectionId = $classes["1A"]; makeClassTeacher = $true
    subjectIds = @($subjects["1A|English"], $subjects["1A|Mathematics"], $subjects["1A|Science"])
} | Out-Null
Invoke-Api POST "/api/teachers/$arjun/subjects" @{
    classSectionId = $classes["5A"]; makeClassTeacher = $true
    subjectIds = @($subjects["5A|English"], $subjects["5A|Mathematics"], $subjects["5A|Science"])
} | Out-Null
Step "Created 4 staff users (password: $StaffPassword) and assigned teachers"

# ---- applications --------------------------------------------------------------------------------
$today = (Get-Date).ToString("yyyy-MM-dd")
$serial = 0

function New-Application($First, $Last, $Gender, $Dob, $ClassKey, $FatherName, $FatherMobile, $MotherName, $MotherMobile) {
    Invoke-Api POST "/api/admissions" @{
        firstName = $First; lastName = $Last; gender = $Gender; dateOfBirth = $Dob
        appliedForClassSectionId = $classes[$ClassKey]; admissionType = "New"
        phone = $FatherMobile; email = $null; remarks = $null
        guardians = @(
            @{ relationType = "Father"; name = $FatherName; mobile = $FatherMobile; isPrimaryContact = $true },
            @{ relationType = "Mother"; name = $MotherName; mobile = $MotherMobile; isPrimaryContact = $false })
    }
}

function Confirm-Application($Id) {
    Invoke-Api POST "/api/admissions/$Id/confirm-admission" @{
        admissionFee = 5000; admissionFeeReference = "RCPT-DEMO-$Id"; bloodGroup = "O+"
        religion = $null; category = $null; medicalNotes = $null; remarks = $null
    } | Out-Null
}

function Enrol-Application($Id, $ClassKey, $Roll) {
    $script:serial++
    $guardianMatches = Invoke-Api GET "/api/admissions/$Id/guardian-matches"
    $decisions = @()
    foreach ($g in $guardianMatches.guardians) {
        if ($g.candidates.Count -gt 0) {
            $decisions += @{ admissionGuardianId = $g.admissionGuardianId; action = "UseExisting"; parentId = $g.candidates[0].parentId }
        }
        else {
            $decisions += @{ admissionGuardianId = $g.admissionGuardianId; action = "CreateNew"; parentId = $null }
        }
    }
    Invoke-Api POST "/api/admissions/$Id/enroll" @{
        rollNumber = $null; admissionNumber = $null   # blank: the server generates both
        admissionDate = $today; entryPoint = $null; transportRequired = $false
        allottedClassSectionId = $classes[$ClassKey]
        nationality = "Indian"; curriculumTrack = $null; englishProficiency = $null; ealCode = $null; house = $null; allergies = $null
        guardians = $decisions
    } | Out-Null
}

$a1 = New-Application "Aarav" "Kumar" "Male" "2019-04-12" "1A" "Rajesh Kumar" "9876501111" "Priya Kumar" "9876502222"
Confirm-Application $a1.id; Enrol-Application $a1.id "1A" "1A001"

$a2 = New-Application "Diya" "Kumar" "Female" "2015-09-03" "5A" "Rajesh Kumar" "9876501111" "Priya Kumar" "9876502222"
Confirm-Application $a2.id; Enrol-Application $a2.id "5A" "5A001"     # sibling: re-uses the same parents

$a3 = New-Application "Mohammed" "Al Farsi" "Male" "2019-01-25" "1A" "Khalid Al Farsi" "9876503333" "Mariam Al Farsi" "9876504444"
Confirm-Application $a3.id; Enrol-Application $a3.id "1A" "1A002"

$a4 = New-Application "Sara" "Thomas" "Female" "2015-06-18" "5A" "George Thomas" "9876505555" "Anu Thomas" "9876506666"
Confirm-Application $a4.id; Enrol-Application $a4.id "5A" "5A002"

$a5 = New-Application "Rohan" "Nair" "Male" "2019-11-02" "1B" "Suresh Nair" "9876507777" "Lakshmi Nair" "9876508888"
Confirm-Application $a5.id                                           # admitted, not yet enrolled

New-Application "Ishita" "Menon" "Female" "2015-02-14" "5A" "Vinod Menon" "9876509999" "Deepa Menon" "9876500000" | Out-Null   # registered only

$a7 = New-Application "Karthik" "Raman" "Male" "2019-08-30" "1B" "Mohan Raman" "9876511111" "Gita Raman" "9876522222"
Invoke-Api POST "/api/admissions/$($a7.id)/reject" @{ rejectionReason = "Age below the minimum for Class 1." } | Out-Null
Step "Created 7 applications: 4 enrolled students, 1 admitted, 1 registered, 1 rejected"

Write-Host ""
Write-Host "Done. Sign in as $AdminUser (all rights), teacher.priya (class teacher of Class 1 A), teacher.arjun (Class 5 A)," -ForegroundColor Green
Write-Host "demo.clerk or demo.supervisor, with password $StaffPassword." -ForegroundColor Green
