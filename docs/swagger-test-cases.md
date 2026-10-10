# Swagger / API Test Cases (Full Backend)

Run these in Swagger (`https://localhost:7100/swagger`) top to bottom. Each section creates
the data the next section needs. Total: **~170 cases**.

## 0. Conventions

**Setup**

1. Start the API. Open Swagger.
2. `POST /api/auth/login` as your Admin, copy `token`, click **Authorize**, paste `Bearer <token>` (or just the token, depending on your Swagger setup).
3. Use the prefix **ZT** (zip-test) in every name so the data is easy to find and remove.
4. Keep a scratch list of ids as you go: `{yearId}`, `{classA}`, `{classB}`, `{classFull}`, `{classInactive}`, `{subjectMath}`, `{teacher1}`, `{parentFather}`, `{adm1}`, `{student1}`, … Each test says what to save.

**Standard error bodies**

| HTTP | `errorCode` | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | FluentValidation failed; body has `errors: { field: [messages] }` |
| 401 | `UNAUTHENTICATED` | no/expired/invalid token |
| 403 | `FORBIDDEN` | logged in but missing permission |
| 404 | `NOT_FOUND` | id does not exist (or is soft-deleted) |
| 409 | `CONFLICT` | duplicate, inactive class, class full, wrong status, blocked delete |
| 422 | `BUSINESS_RULE_VIOLATION` | a business rule (e.g. missing guardian decision) |
| 500 | `INTERNAL_SERVER_ERROR` | **always a bug** — report with the `traceId` |

Every error body also has `success:false`, `message`, `traceId`. A 500 must never leak a stack trace or SQL.

**Result legend**: ✅ pass / ❌ fail / ⏭ skipped. Copy this file and tick as you go.

---

## 1. Authentication

| ID | Request | Expected |
|---|---|---|
| AUTH-01 | `POST /api/auth/login` valid username + password | 200, `token`, `roles`, `expiresAtUtc` in the future |
| AUTH-02 | Same with **email** in `usernameOrEmail` | 200 |
| AUTH-03 | Wrong password | 401 (no token) |
| AUTH-04 | Unknown user | 401 (same message as AUTH-03: must not reveal which part was wrong) |
| AUTH-05 | Empty `usernameOrEmail` and `password` | 400 `VALIDATION_ERROR` with both fields listed |
| AUTH-06 | Login for a user whose status is `Inactive` or `Suspended` | 401 |
| AUTH-07 | Any protected endpoint with **no** token (e.g. `GET /api/students`) | 401 `UNAUTHENTICATED` |
| AUTH-08 | Protected endpoint with garbage token `abc` | 401 |
| AUTH-09 | Decode the token at jwt.io | contains `permission` claims matching the role (Admin: 11 permissions) |

## 2. Users (`POST /api/users`, needs `Users.Create`)

Create the helper users you need later (save usernames/passwords):

```json
{ "username": "zt.clerk", "email": "zt.clerk@test.local", "password": "Test@1234", "roleName": "Clerk" }
```
Also create `zt.supervisor` (role `Supervisor`).

| ID | Case | Expected |
|---|---|---|
| USR-01 | Create `zt.clerk` (Clerk) | 201, response has no password/hash |
| USR-02 | Create `zt.supervisor` (Supervisor) | 201 |
| USR-03 | Duplicate username | 409 |
| USR-04 | Duplicate email (different username) | 409 |
| USR-05 | Unknown `roleName` ("Nope") | 404 |
| USR-06 | Password `abc` | 400, messages for length, uppercase, digit, special |
| USR-07 | Username with space/`@` | 400 "only letters, digits, dots, underscores, hyphens" |
| USR-08 | Invalid email | 400 |
| USR-09 | Empty body fields | 400 listing all required fields |
| USR-10 | Call as `zt.clerk` (no `Users.Create`) | 403 `FORBIDDEN` |

## 3. Class Sections (`/api/class-sections`, write needs `ClassSections.Manage`)

Bodies:

```json
{ "name": "ZT Class", "section": "A", "grade": 1, "stage": "Primary", "medium": "English",
  "stream": "General", "capacity": 30, "building": "Main", "floor": 1, "room": "101",
  "academicYearId": null, "isActive": true }
```
Create: **ZT Class A** (cap 30, save `{classA}`), **ZT Class B** (section "B", cap 30, `{classB}`), **ZT Class Full** (section "C", capacity **1**, `{classFull}`), **ZT Class Off** (section "D", `"isActive": false`, `{classInactive}`).

| ID | Case | Expected |
|---|---|---|
| CLS-01 | Create A with `academicYearId: null` | 201; year = current (2026-2027); `enrolled` = 0; display name "ZT Class A (2026-2027)" |
| CLS-02 | Create B, Full, Off | 201 each |
| CLS-03 | Duplicate name+section+year | 409 |
| CLS-04 | Same name+section with extra spaces (" ZT Class ", " A ") | 409 (values are trimmed; letter-case matching depends on the SQL Server collation, normally case-insensitive — record what you see) |
| CLS-05 | `capacity` 0, 201 | 400 "between 1 and 200" |
| CLS-06 | `grade` 13 | 400 "between 1 and 12" |
| CLS-07 | Invalid `stage` ("Middle"), `medium` ("French"), `stream` ("Arts") | 400 each, correct message |
| CLS-08 | `floor` 21 / -1 | 400 |
| CLS-09 | Empty name / empty section | 400 |
| CLS-10 | `academicYearId` 9999 | 404 |
| CLS-11 | `GET /api/class-sections` | 200 list contains the four; `enrolled` counts present |
| CLS-12 | `GET /api/class-sections/{id}` valid / 9999 | 200 / 404 |
| CLS-13 | `PUT` change room/building | 200, fields updated |
| CLS-14 | `PUT` rename to an existing name+section | 409 |
| CLS-15 | `PUT` capacity lower than current students (do after §9: class with 2 students, set capacity 1) | 409 "Capacity cannot be lower than the current strength" |
| CLS-16 | `PATCH /{id}/status` `{ "isActive": false }` then `true` | 200, status toggles |
| CLS-17 | `DELETE` an empty class (create a throwaway "ZT Temp") | 204; then `GET` it → 404 |
| CLS-18 | `DELETE` a class that has students/admissions/subjects (after §4/§7) | 409 "Mark it Inactive instead" with counts |
| CLS-19 | Create/Put/Patch/Delete as `zt.clerk` | 403 |
| CLS-20 | `GET` list as `zt.clerk` | 200 (read needs only login) |

## 4. Subjects (`/api/subjects`, write needs `Subjects.Manage`)

```json
{ "name": "ZT Math", "code": "ZTMATH", "classSectionId": {classA}, "type": "Theory",
  "maxMarks": 100, "passMarks": 35, "theoryMax": null, "theoryPass": null,
  "practicalMax": null, "practicalPass": null, "isActive": true }
```
Save `{subjectMath}`. Also create `ZT Science` type `Both` (theoryMax 70/theoryPass 25, practicalMax 30/practicalPass 10, `{subjectSci}`) in class A, and `ZT Art` in class B (`{subjectArt}`).

| ID | Case | Expected |
|---|---|---|
| SUB-01 | Create Math (Theory) | 201 |
| SUB-02 | Create Science (Both) | 201 |
| SUB-03 | Create Art in class B | 201 |
| SUB-04 | Empty `code` with name "ZT History" | 201, code auto-generated from name |
| SUB-05 | Same code in same class | 409 "Subject Code + Class must be unique" |
| SUB-06 | Same code in a different class | 201 |
| SUB-07 | Type `Theory` without `maxMarks` | 400 "Max Marks is required" |
| SUB-08 | Type `Both` without `theoryMax` | 400 |
| SUB-09 | Type "Oral" | 400 "Type must be Theory, Practical, or Both" |
| SUB-10 | `classSectionId` 0 / 9999 | 400 / 404 |
| SUB-11 | `GET` list, `GET /{id}`, `GET /9999` | 200 / 200 / 404 |
| SUB-12 | `PUT` change marks | 200 |
| SUB-13 | `PUT` code to one that exists in same class | 409 |
| SUB-14 | `DELETE` Art | 204; then `GET` → 404 |
| SUB-15 | Write as `zt.clerk` | 403 |

## 5. Teachers (`/api/teachers`, write needs `Teachers.Manage`)

```json
{ "name": "ZT Teacher One", "username": "zt.teacher1", "email": "zt.teacher1@test.local",
  "password": "Test@1234", "phone": "9876500001", "specialization": "Maths", "status": "Active" }
```
Save `{teacher1}`. Create `zt.teacher2` as well (`{teacher2}`).

| ID | Case | Expected |
|---|---|---|
| TCH-01 | Create teacher 1 | 201; a user with role `Teacher` exists (`login` works, AUTH) |
| TCH-02 | Create teacher 2 | 201 |
| TCH-03 | Duplicate username / email | 409 / 409 |
| TCH-04 | Weak password | 400 |
| TCH-05 | Status "Retired" | 400 "Active, Inactive or Suspended" |
| TCH-06 | `GET` list shows stats (subjects count, class-teacher-of) | 200 |
| TCH-07 | `GET /{id}`, `/9999` | 200 / 404 |
| TCH-08 | `PUT` update name/phone, no `newPassword` | 200; login still works with the old password |
| TCH-09 | `PUT` with `newPassword` weak / strong | 400 / 200 (login with new works) |
| TCH-10 | `PATCH /{id}/status` `Suspended` → try logging in as that teacher | 200 then login 401; set back `Active` |
| TCH-11 | `POST /{id}/subjects` `{ classSectionId:{classA}, subjectIds:[{subjectMath}] }` | 200; assignment list shows Math |
| TCH-12 | Assign subject of class B while sending class A | 409 "do not belong to" |
| TCH-13 | Assign unknown subject id | 404 |
| TCH-14 | Assign Math to teacher 2 (already taught by teacher 1) | 409 "Already taught by another teacher" |
| TCH-15 | Empty `subjectIds` | 400 |
| TCH-16 | `PUT /{id}/class-teacher` for teacher 2 in class A (teaches nothing there) | 409 "Assign at least one subject first" |
| TCH-17 | `PUT /{teacher1}/class-teacher` `{classSectionId:{classA}}` | 200 |
| TCH-18 | Same for teacher 2 on class A after teacher 1 is class teacher | 409 "already has a class teacher" (assign teacher 2 `ZT Science` first) |
| TCH-19 | `GET /{id}/assignments` | 200 lists subjects + class-teacher flag |
| TCH-20 | `DELETE /{id}/class-teacher/{classId}` for a class they are not teacher of | 409 |
| TCH-21 | `DELETE /{teacher1}` while assigned | 409 with counts |
| TCH-22 | Remove class teacher then `DELETE /{id}/subjects/{subjectMath}` | 200; subject unassigned |
| TCH-23 | `DELETE /{id}/subjects/{subject}` not assigned to that teacher | 404 |
| TCH-24 | `DELETE` an unassigned throwaway teacher | 204; login for that user now 401 |
| TCH-25 | Write calls as `zt.clerk` | 403 |

> After TCH-22 re-assign teacher 1 to Math and make them class teacher of A again — attendance tests need it.

## 6. Parents (`/api/parents`, write needs `Parents.Manage`)

```json
{ "name": "ZT Father", "email": "zt.father@test.local", "mobile": "9876501111",
  "occupation": "Engineer", "nationality": "Indian", "countryOfResidence": null, "timezone": null,
  "preferredLanguage": "English", "preferredContactMethod": "Phone", "whatsapp": null,
  "emergencyOnly": false, "notifyAttendance": true, "notifyExams": true, "notifyFees": true,
  "notifyNotices": true, "notifyDiscipline": false, "employer": null, "jobTitle": null,
  "workEmail": null, "workPhone": null, "billingContact": true,
  "addressLine": null, "city": null, "state": null, "pincode": null }
```
Create `ZT Father` (`{parentFather}`, mobile 9876501111) and `ZT Mother` (`{parentMother}`, mobile 9876502222).

| ID | Case | Expected |
|---|---|---|
| PAR-01 | Create father, mother | 201 each |
| PAR-02 | Missing `name` or `mobile` | 400 |
| PAR-03 | Invalid email | 400 |
| PAR-04 | Create another parent with the **same mobile** as father | 201 (allowed by design; matching handles it) |
| PAR-05 | `GET` list, `GET /{id}`, `/9999` | 200 / 200 / 404 |
| PAR-06 | `PUT` change occupation | 200 |
| PAR-07 | `DELETE` the PAR-04 duplicate | 204; `GET` → 404 |
| PAR-08 | Write as `zt.clerk` | 403 |

(Linking tests: see §11 after students exist.)

## 7. Admissions (`/api/admissions`, write needs `Admissions.Manage`)

Register body:

```json
{ "firstName": "ZTKid", "middleName": null, "lastName": "One", "gender": "Male",
  "dateOfBirth": "2018-05-10", "appliedForClassSectionId": {classA}, "admissionType": "New",
  "previousSchool": null, "phone": "9876501111", "email": null,
  "addressLine": null, "city": null, "state": null, "pincode": null, "remarks": null,
  "guardians": [
    { "relationType": "Father", "name": "ZT Father", "mobile": "9876501111", "email": null, "isPrimaryContact": true },
    { "relationType": "Mother", "name": "ZT Mother", "mobile": "9876502222", "email": null, "isPrimaryContact": false }
  ] }
```
Save id as `{adm1}`.

| ID | Case | Expected |
|---|---|---|
| ADM-01 | Register `{adm1}` with 2 guardians | 201, `status:"Registered"`, `regNo` like `REG-2026-00xx`, 2 guardians in response |
| ADM-02 | Register with **no guardians** (`null`), save `{adm2}` | 201 (guardian is only required at enrolment) |
| ADM-03 | DOB in the future | 400 |
| ADM-04 | `admissionType` "Walk-in" | 400 "New or Transfer" |
| ADM-05 | Missing `phone` | 400 |
| ADM-06 | Invalid email | 400 |
| ADM-07 | 6 guardians | 400 "at most 5" |
| ADM-08 | Two fathers | 400 "only one father and one mother" |
| ADM-09 | Two primary contacts | 400 "Only one guardian can be the primary contact" |
| ADM-10 | Guardian `relationType` "Uncle" | 400 |
| ADM-11 | Guardian with empty name / bad mobile `abc` | 400 |
| ADM-12 | `appliedForClassSectionId` = `{classInactive}` | 409 "inactive" |
| ADM-13 | `appliedForClassSectionId` 9999 | 404 |
| ADM-14 | `GET` list (newest first), `GET /{id}`, `/9999` | 200 / 200 / 404 |
| ADM-15 | `PUT /{adm1}` change phone and replace guardians (1 guardian) | 200; guardians exactly as sent |
| ADM-16 | `PUT` to restore the two guardians again | 200 |
| ADM-17 | `PUT` change applied class to inactive class | 409; keep the same inactive class on an old applicant → allowed |
| ADM-18 | `POST /{adm1}/confirm-admission` `{ "admissionFee": 500, "admissionFeeReference": "RCPT-1", "bloodGroup": "O+", "religion": "Hindu", "category": "General", "medicalNotes": null, "remarks": "ok" }` | 200, `status:"Admitted"`, fee saved |
| ADM-19 | Confirm again | 409 "expected 'Registered'" |
| ADM-20 | Confirm with `admissionFee` -5 | 400 |
| ADM-21 | `reject` `{adm2}` with empty reason | 400 |
| ADM-22 | `reject` `{adm2}` `{ "rejectionReason": "ZT test" }` | 200, `Rejected` |
| ADM-23 | Reject again / reject an Enrolled one (later) | 409 |
| ADM-24 | Enrol `{adm2}` (rejected) | 409 "expected 'Admitted'" |
| ADM-25 | Enrol `{adm?}` still Registered | 409 |
| ADM-26 | `DELETE` a throwaway admission | 204; `GET` → 404 |
| ADM-27 | Write calls as `zt.clerk` | 403; `GET` list 200 |
| ADM-28 | `GET /{id}/guardian-matches` as `zt.clerk` | 403 |

## 8. Guardian matching and enrolment

Pre-conditions: `{adm1}` is **Admitted**. Parents `ZT Father` (9876501111) and `ZT Mother` (9876502222) exist.

| ID | Case | Expected |
|---|---|---|
| ENR-01 | `GET /{adm1}/guardian-matches` | 200; father → `suggestedAction:"UseExisting"` with candidate `{parentFather}`; mother → `UseExisting` `{parentMother}` |
| ENR-02 | Matches for admission whose mobile matches **no** parent | `CreateNew`, empty candidates |
| ENR-03 | Make two parents share one mobile, then match | `MustChoose` with 2 candidates |
| ENR-04 | Guardian without mobile (edit admission before it was Admitted, or use a Registered one with no mobile and move to Admitted) | `MissingMobile` |
| ENR-05 | Matching an admission that is not `Admitted` | 409 |
| ENR-06 | Different formatting of the same number (`+91 98765-01111` vs `9876501111`) | still matched (digits-only key) |

Enrol body (use the ids from ENR-01 for `admissionGuardianId`):

```json
{ "rollNumber": "ZT01", "admissionNumber": "ZT-ADM-001", "admissionDate": "2026-10-08",
  "entryPoint": null, "transportRequired": false, "allottedClassSectionId": {classA},
  "nationality": null, "curriculumTrack": null, "englishProficiency": null,
  "ealCode": null, "house": null, "allergies": null,
  "guardians": [
    { "admissionGuardianId": {gFather}, "action": "UseExisting", "parentId": {parentFather} },
    { "admissionGuardianId": {gMother}, "action": "UseExisting", "parentId": {parentMother} } ] }
```

Negative tests first (each must leave the database unchanged — verify with `GET /api/students` and SSMS):

| ID | Change to the body | Expected |
|---|---|---|
| ENR-10 | `guardians: []` | 422 "A decision is required…" |
| ENR-11 | Only one of two decisions | 422 "A decision is required for every guardian. Missing for: …" |
| ENR-12 | Same guardian decided twice | 422 / 400 "Each guardian can have only one decision" |
| ENR-13 | Decision for a guardian id from another admission | 422 "not on this application" |
| ENR-14 | `UseExisting` with no `parentId` | 400 |
| ENR-15 | `CreateNew` with a `parentId` | 400 |
| ENR-16 | `UseExisting` with parent id 9999 | 404 |
| ENR-17 | Father and mother both `UseExisting` the **same** parent | 422 "same parent cannot be linked as two different guardians" |
| ENR-18 | Action "Maybe" | 400 |
| ENR-19 | `rollNumber` empty / 21 chars | 400 |
| ENR-20 | `allottedClassSectionId` = `{classInactive}` | 409 inactive |
| ENR-21 | Allot to `{classFull}` after it already has 1 student | 409 "is full (1/1)" |
| ENR-22 | Roll number already used in that class | 409 |
| ENR-23 | `admissionNumber` already used | 409 |
| ENR-24 | A guardian has no mobile | 422 "A mobile number is required before enrolling" |
| ENR-25 | Enrol as `zt.clerk` | 403 |

Positive:

| ID | Case | Expected |
|---|---|---|
| ENR-30 | Valid enrol of `{adm1}` | 200; `status:"Enrolled"`, `studentId` set. Save `{student1}` |
| ENR-31 | `GET /api/students/{student1}/enrollments` | exactly 1 row: Active, class A, roll ZT01, start = admission date |
| ENR-32 | `GET /api/students/{student1}/guardians` | 2 links (Father, Mother); father is primary |
| ENR-33 | Enrol again | 409 (status now Enrolled) |
| ENR-34 | Enrol a new admission using `CreateNew` for a guardian with a new mobile | 200; new Parent row appears in `GET /api/parents` |
| ENR-35 | Enrol an admission whose guardian shares a mobile with two parents, sending `UseExisting` with one chosen | 200 |
| ENR-36 | Class A `enrolled` count in `GET /api/class-sections/{classA}` | +1 |
| ENR-37 | Atomic check: cause a failure after partial work (e.g. admission number duplicate) and confirm **no** student, parent or guardian rows were created | pass |

## 9. Students (`/api/students`)

Create standalone body (admission-less student):

```json
{ "admNo": "ZT-STU-002", "rollNumber": "ZT02", "classSectionId": {classA}, "admissionDate": "2026-10-08",
  "photoUrl": null, "firstName": "ZTKid", "middleName": null, "lastName": "Two", "gender": "Female",
  "dateOfBirth": "2017-03-03", "mobile": null, "email": null, "addressLine": null, "city": null,
  "state": null, "pincode": null, "category": "General", "religion": null, "previousSchool": null,
  "transportRequired": false, "transportRoute": null, "nationality": null, "secondNationality": null,
  "countryOfBirth": null, "preferredName": null, "motherTongue": null, "homeLanguage": null,
  "englishProficiency": null, "curriculumTrack": null, "admissionType": "Fresh Admission",
  "custodyArrangement": null, "mediaConsent": true, "house": null, "ealCode": null,
  "feeConcessionPercent": null, "health": null, "pickupPersons": null }
```
Save as `{student2}`.

| ID | Case | Expected |
|---|---|---|
| STU-01 | Create `{student2}` | 201; enrollment row created automatically |
| STU-02 | Duplicate `admNo` | 409 |
| STU-03 | Duplicate roll number in same class | 409 "Roll number … already in use in this class" |
| STU-04 | Class inactive / full | 409 |
| STU-05 | Missing first name / empty gender / future DOB | 400 (gender is only checked for non-empty today, so `"Robot"` is accepted — noted as a gap) |
| STU-06 | `feeConcessionPercent` 150 and -1 | 400 "between 0 and 100" |
| STU-07 | `GET /api/students` | 200 summaries (no health/identity fields) |
| STU-08 | `GET /{id}` for `{student1}` | 200 detail with `health`, `identity`, `pickupPersons`, `guardians` |
| STU-09 | `GET /9999` | 404 |
| STU-10 | `PUT /{student2}` add `health` `{ "bloodGroup":"A+", "allergies":"Peanuts", "dietaryRequirements":null, "medicalNotes":null, "specialEducationalNeeds":null, "insuranceProvider":null, "insurancePolicyExpiry":null }` | 200; `GET` shows health |
| STU-11 | `PUT` with `health: null` | health unchanged |
| STU-12 | `PUT` with `pickupPersons: [ { "name":"Aunt", "relation":"Aunt", "phone":"9876503333", "idNote":"Emirates ID" } ]` | 200; listed on detail |
| STU-13 | `PUT` pickup person without phone / invalid phone | 400 |
| STU-14 | `PUT` with `pickupPersons: []` | all pickup persons removed |
| STU-14b | `PUT` with 11 pickup persons | 400 (limit is 10) |
| STU-15 | `PUT` with `pickupPersons: null` | unchanged |
| STU-16 | `PUT` change status to `Inactive` and back | 200; enrollment stays Active |
| STU-17 | `PUT` status "Passed Out" | 400 (only Active, Inactive, Left are accepted) |
| STU-18 | `DELETE /{id}` a throwaway student | 204; `GET` → 404; its guardians/health no longer visible |
| STU-19 | Write calls as `zt.clerk` | 403; reads 200 |

**Identity & masking**

| ID | Case | Expected |
|---|---|---|
| IDN-01 | `PUT /{student2}/identity` `{ "aadhaarNumber":"123456789012", "passportNumber":"P1234567", "passportExpiry":"2030-01-01", "visaType":"Resident", "visaExpiry":"2028-01-01" }` as Admin | 200 |
| IDN-02 | `GET /{student2}` as Admin | identity shows full numbers, `isMasked:false` |
| IDN-03 | `GET /{student2}` as `zt.clerk` | `aadhaarNumber` like `********9012`, passport masked, `isMasked:true` |
| IDN-04 | Same as Supervisor (has ViewSensitive) | full numbers |
| IDN-05 | `PUT /identity` as `zt.clerk` | 403 |
| IDN-06 | Aadhaar with 11 digits / letters | 400 "exactly 12 digits" |
| IDN-07 | Same Aadhaar on another student | 409 "already recorded for another student" |
| IDN-08 | Aadhaar with spaces `1234 5678 9012` | 200, stored without spaces |
| IDN-09 | `PUT /identity` with all fields null | clears the document values |
| IDN-10 | Identity of unknown student | 404 |

## 10. Academic history (enrollments)

Pre-condition: `{student1}` Active in class A (roll ZT01); class B is Active with room.

| ID | Case | Expected |
|---|---|---|
| HIS-01 | `GET /{student1}/enrollments` | 1 Active row |
| HIS-02 | `GET /9999/enrollments` | 404 |
| HIS-03 | `PUT` student1 with only `rollNumber: "ZT10"` (class unchanged) | 200; **still 1 row**, roll updated in place |
| HIS-04 | `PUT` student1 `classSectionId:{classB}`, roll `ZT01` | 200; history now 2 rows: newest = class B Active (start today); older = class A `Transferred`, end date today, remarks "Moved to ZT Class B (2026-2027)"; `GET /students/{id}` shows class B |
| HIS-05 | Move into inactive class | 409 |
| HIS-06 | Move into full class | 409 |
| HIS-07 | Move to a class where the roll is taken | 409 |
| HIS-08 | Move to a class in **another academic year** (see SQL below) | 422 "…year-end promotion" |
| HIS-09 | `PUT` status `Left` and also change class in the same call | 422 "cannot be moved … and marked as Left in the same edit" |
| HIS-10 | `PUT` status `Left` (class unchanged) | 200; Active row becomes `Left` with end date today; student's class/roll unchanged; no Active row exists |
| HIS-11 | `GET /api/class-sections/{class}` | `enrolled` decreased (Left students not counted) |
| HIS-12 | `PUT` status back to `Active` | 200; a **new** Active row opens; history = Left row + new Active row |
| HIS-13 | Reactivating into a class that is now inactive/full | 409 |
| HIS-14 | After HIS-04, in SSMS: `SELECT * FROM StudentEnrollments WHERE StudentId = {student1}` | exactly one `Active` row; check constraints satisfied |
| HIS-15 | Attendance roster of class A after the move | student1 no longer in roster of A (appears in B) |

Cross-year setup (for HIS-08), run once in SSMS:

```sql
INSERT INTO AcademicYears (Name, StartDate, EndDate, IsCurrent, Status, CreatedAt, UpdatedAt)
VALUES ('2027-2028', '2027-06-01', '2028-03-31', 0, 'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
SELECT Id FROM AcademicYears WHERE Name = '2027-2028';   -- use as academicYearId
```
then `POST /api/class-sections` with that `academicYearId` (name "ZT Class Next", section "A").

## 11. Guardian links (parent ↔ student)

| ID | Case | Expected |
|---|---|---|
| LNK-01 | `POST /api/parents/{parentFather}/students` `{ "studentId":{student2}, "relationType":"Father", "isPrimaryContact":true }` | 201 |
| LNK-02 | Same link again | 409 "already linked" |
| LNK-03 | `POST /api/students/{student2}/guardians` `{ "parentId":{parentMother}, "relationType":"Mother", "isPrimaryContact":false }` | 201 |
| LNK-04 | Relation "Cousin" | 400 |
| LNK-05 | Unknown parent / student id | 404 |
| LNK-06 | `GET /api/parents/{parentFather}/students` | lists student2 (+ student1) |
| LNK-07 | `GET /api/students/{student2}/guardians` | 2 rows |
| LNK-08 | `POST /api/parents/{parentMother}/students/{student2}/primary` | 204; mother primary, father no longer |
| LNK-09 | Link mother as primary while father is primary using the link endpoint with `isPrimaryContact:true` | success moves primary, or 409 — **record which**; there must never be two primaries (check SQL) |
| LNK-10 | `DELETE /api/students/{student2}/guardians/{parentFather}` | 204; link gone |
| LNK-11 | `DELETE` a link that does not exist | 404 |
| LNK-12 | Soft-delete the parent (`DELETE /api/parents/{id}`) → `GET /api/students/{id}/guardians` | that guardian no longer listed |
| LNK-13 | Link calls as `zt.clerk` | 403 |

## 12. Attendance (`/api/attendance`)

Pre-conditions: teacher 1 is **class teacher of class A** and teaches Math; class A has ≥2 Active students (`{student1}` moved back to A or create more); users: Admin, `zt.clerk` (Attendance.Manage), `zt.teacher1` (Attendance.Mark), `zt.teacher2` (not class teacher of A).

Roster call: `GET /api/attendance/roster?classSectionId={classA}&date=2026-10-08`

Save body:

```json
{ "classSectionId": {classA}, "date": "2026-10-08", "subjectId": null,
  "records": [ { "studentId": {s1}, "status": "Present", "remarks": null },
               { "studentId": {s2}, "status": "Late", "remarks": "Bus" } ] }
```

| ID | Case | Expected |
|---|---|---|
| ATT-01 | Roster as Admin, today | 200; `isSaved:false`, `canEdit:true`, Active students listed |
| ATT-02 | Save all students | 200; `isSaved:true`, `savedBy` = username |
| ATT-03 | Roster again | statuses and remarks come back; last-7 dots array present |
| ATT-04 | Save again with a changed status | 200; still one session (check `AttendanceSessions` count) |
| ATT-05 | Save with one Active student missing | 422 "N student(s) have no status yet: …" |
| ATT-06 | Save with a student from another class | 422 "Some students do not belong to this class" |
| ATT-07 | Duplicate student in `records` | 400 "A student appears more than once" |
| ATT-08 | Status "Sick" | 400 "Present, Absent, Late, Half Day or Leave" |
| ATT-09 | Empty `records` | 400 |
| ATT-10 | `remarks` of 251 chars | 400 |
| ATT-11 | Future date | roster `canEdit:false` with reason; save → 422 "cannot be marked for a future date" |
| ATT-12 | Date 8+ days ago as Clerk | roster `canEdit:false`; save → 422 "Only an Admin can change attendance older than 7 days" |
| ATT-13 | Same old date as Admin | 200 |
| ATT-14 | Date exactly 7 days ago as Clerk | allowed |
| ATT-15 | Subject attendance: `subjectId:{subjectMath}` as Admin | 200 and it is a **separate** session from the daily one |
| ATT-16 | `subjectId` that belongs to class B but `classSectionId` = A | 422 "does not belong to the selected class" |
| ATT-17 | `subjectId` 9999 / `classSectionId` 9999 | 404 |
| ATT-18 | Daily attendance as `zt.teacher1` (class teacher of A) | 200 |
| ATT-19 | Daily attendance for class A as `zt.teacher2` | 403 "Only the class teacher can take daily attendance" |
| ATT-20 | Math attendance as `zt.teacher1` | 200 |
| ATT-21 | Math attendance as `zt.teacher2` | 403 "Only the teacher assigned to this subject…" |
| ATT-22 | Any save as a user with neither permission | 403 |
| ATT-23 | Roster as `zt.teacher2` for class A | 200 but `canEdit:false` + reason |
| ATT-24 | Left student in class: set student Left after attendance saved; roster | student still shown for old date (already recorded), not for new dates |
| ATT-25 | `GET /attendance/students/{id}?from=2026-10-01&to=2026-10-31` | 200 per-day records + counts + percentage |
| ATT-26 | `from` after `to` | 422 |
| ATT-27 | Range over 366 days | 422 |
| ATT-28 | `GET /attendance/summary?classSectionId={classA}&from=…&to=…` | 200 per-student counts and %; check formula: Present=1, Late=1, Half Day=0.5, Absent=0, Leave excluded |
| ATT-29 | Summary for unknown class | 404 |
| ATT-30 | Percentage with all `Leave` | `null` (nothing counts) |

## 13. Authorization matrix (repeat after any permission change)

Log in as each user, **re-login after changing permissions** (permissions live in the token).

| Endpoint group | Admin | Supervisor | Clerk | Teacher | No token |
|---|:-:|:-:|:-:|:-:|:-:|
| Any `GET` (non-sensitive) | 200 | 200 | 200 | 200 | 401 |
| Class sections / subjects / teachers / parents / admissions / students **writes** | 2xx | 403 | 403 | 403 | 401 |
| `GET /admissions/{id}/guardian-matches` | 200 | 403 | 403 | 403 | 401 |
| `POST /api/users` | 2xx | 403 | 403 | 403 | 401 |
| `PUT /students/{id}/identity` | 200 | 403 (no Students.Manage) | 403 | 403 | 401 |
| Student detail identity fields | full | full | masked | masked | 401 |
| Attendance save (any class) | ✔ | ✔ | ✔ | own class/subject only | 401 |

| ID | Case |
|---|---|
| AZ-01 | Run the table above with one endpoint per row for each user. Any cell that differs is a bug. |
| AZ-02 | Remove `Students.ViewSensitive` from Supervisor in SSMS (`DELETE FROM RolePermissions WHERE RoleId=2 AND PermissionId=11`): old token still sees full values until re-login; new login sees masked. Restore afterwards. |

## 14. Cross-cutting checks

| ID | Check | Expected |
|---|---|---|
| X-01 | Every 4xx/5xx body has `success`, `message`, `errorCode`, `traceId` | yes |
| X-02 | Take a `traceId` from an error and search the Serilog log file | request, status and duration lines found |
| X-03 | Force a 500 (e.g. stop SQL Server briefly) | generic message, no stack trace/SQL in the body |
| X-04 | Send `Authorization: Bearer` with an expired token | 401 `UNAUTHENTICATED` |
| X-05 | Send an `id` that is a string (`/api/students/abc`) | 404 (route constraint) |
| X-06 | Send malformed JSON | 400 |
| X-07 | Large list responses (after 100+ rows): `GET /api/students` | responds < 1 s; note there is **no server pagination yet** (filtering is client-side) |
| X-08 | Concurrent double-submit: enrol the same admission twice quickly | one 200, one 409; one student only |
| X-09 | Soft delete visibility: delete a student → `GET /api/attendance/summary` | student no longer counted; rows still exist in SQL |

## 15. Final database checks (SSMS)

```sql
-- one Active enrollment per student, mirror matches Students row
SELECT s.Id, s.ClassSectionId, s.RollNumber, e.ClassSectionId AS EnrClass, e.RollNumber AS EnrRoll
FROM Students s JOIN StudentEnrollments e ON e.StudentId = s.Id AND e.Status = 'Active'
WHERE s.IsDeleted = 0 AND (s.ClassSectionId <> e.ClassSectionId OR s.RollNumber <> e.RollNumber);   -- expect 0 rows

-- Active students without an Active enrollment (should only be Left/Inactive-type cases)
SELECT s.Id, s.Status FROM Students s
WHERE s.IsDeleted = 0 AND s.Status = 'Active'
  AND NOT EXISTS (SELECT 1 FROM StudentEnrollments e WHERE e.StudentId = s.Id AND e.Status = 'Active');   -- expect 0 rows

-- more than one primary guardian per student
SELECT StudentId, COUNT(*) FROM StudentGuardians WHERE IsPrimaryContact = 1 GROUP BY StudentId HAVING COUNT(*) > 1;   -- expect 0 rows

-- orphans
SELECT * FROM AdmissionGuardians g WHERE NOT EXISTS (SELECT 1 FROM Admissions a WHERE a.Id = g.AdmissionId);   -- 0 rows
```

### Cleanup (optional, test data only)

Soft-delete the `ZT` data through the API (students, parents, subjects, teachers, class sections in
that order, because of the delete guards), or restore your database backup.
