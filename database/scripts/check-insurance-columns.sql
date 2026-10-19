-- Read-only. How many students hold data in the two insurance columns (hidden from the screens, columns kept).
-- Run it on each real database before deciding whether to drop them. It changes nothing.
SELECT
    COUNT(*)                                                             AS HealthRows,
    SUM(CASE WHEN h.InsuranceProvider IS NOT NULL AND LTRIM(RTRIM(h.InsuranceProvider)) <> '' THEN 1 ELSE 0 END) AS WithProvider,
    SUM(CASE WHEN h.InsurancePolicyExpiry IS NOT NULL THEN 1 ELSE 0 END) AS WithPolicyExpiry,
    SUM(CASE WHEN (h.InsuranceProvider IS NOT NULL AND LTRIM(RTRIM(h.InsuranceProvider)) <> '')
               OR h.InsurancePolicyExpiry IS NOT NULL THEN 1 ELSE 0 END) AS WithEither
FROM dbo.StudentHealth h;

-- The same, only for students that are not deleted.
SELECT
    COUNT(*) AS LiveStudentsWithInsuranceData
FROM dbo.StudentHealth h
JOIN dbo.Students s ON s.Id = h.StudentId
WHERE s.IsDeleted = 0
  AND ((h.InsuranceProvider IS NOT NULL AND LTRIM(RTRIM(h.InsuranceProvider)) <> '') OR h.InsurancePolicyExpiry IS NOT NULL);
