-- G1a: check the copy of the old address columns into [Addresses]. Run it after the migration
-- and again BEFORE G1c (which drops the old columns). Read-only: it changes nothing.
--
-- 1. Counts per owner table. "Copied" + "NotCopied" = "HadOldAddress". Expect NotCopied = 0 before G1c,
--    or a short list you have re-entered by hand (the forms show the old address as a hint).
--    (SQL Server cannot SUM over an EXISTS, so each row gets its 0/1 flag first, then the counts.)
WITH [OldAddresses] AS (
    SELECT N'Students' AS [Owner],
           CASE WHEN EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[StudentId] = o.[Id]) THEN 1 ELSE 0 END AS [IsCopied]
    FROM [Students] o
    WHERE NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[City])), '') IS NOT NULL
       OR NULLIF(LTRIM(RTRIM(o.[State])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[Pincode])), '') IS NOT NULL
    UNION ALL
    SELECT N'Parents' AS [Owner],
           CASE WHEN EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[ParentId] = o.[Id]) THEN 1 ELSE 0 END AS [IsCopied]
    FROM [Parents] o
    WHERE NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[City])), '') IS NOT NULL
       OR NULLIF(LTRIM(RTRIM(o.[State])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[Pincode])), '') IS NOT NULL
    UNION ALL
    SELECT N'Admissions' AS [Owner],
           CASE WHEN EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[AdmissionId] = o.[Id]) THEN 1 ELSE 0 END AS [IsCopied]
    FROM [Admissions] o
    WHERE NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[City])), '') IS NOT NULL
       OR NULLIF(LTRIM(RTRIM(o.[State])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[Pincode])), '') IS NOT NULL
)
SELECT [Owner],
       COUNT(*) AS [HadOldAddress],
       SUM([IsCopied]) AS [Copied],
       COUNT(*) - SUM([IsCopied]) AS [NotCopied]
FROM [OldAddresses]
GROUP BY [Owner]
ORDER BY [Owner];

-- 2. The rows that were NOT copied, and why. Enter these addresses again on the student / parent /
--    application page (the old text is shown there until G1c), then run this script again.
SELECT N'Students' AS [Owner], o.[Id], o.[IsDeleted], o.[AddressLine], o.[City], o.[State], o.[Pincode],
       CASE WHEN NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NULL THEN N'no address line'
            WHEN NULLIF(LTRIM(RTRIM(o.[City])), '') IS NULL THEN N'no city'
            ELSE N'country not recognised (state / PIN / city did not match)' END AS [Reason]
FROM [Students] o
WHERE (NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[City])), '') IS NOT NULL
   OR NULLIF(LTRIM(RTRIM(o.[State])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[Pincode])), '') IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[StudentId] = o.[Id])
UNION ALL
SELECT N'Parents' AS [Owner], o.[Id], o.[IsDeleted], o.[AddressLine], o.[City], o.[State], o.[Pincode],
       CASE WHEN NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NULL THEN N'no address line'
            WHEN NULLIF(LTRIM(RTRIM(o.[City])), '') IS NULL THEN N'no city'
            ELSE N'country not recognised (state / PIN / city did not match)' END AS [Reason]
FROM [Parents] o
WHERE (NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[City])), '') IS NOT NULL
   OR NULLIF(LTRIM(RTRIM(o.[State])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[Pincode])), '') IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[ParentId] = o.[Id])
UNION ALL
SELECT N'Admissions' AS [Owner], o.[Id], o.[IsDeleted], o.[AddressLine], o.[City], o.[State], o.[Pincode],
       CASE WHEN NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NULL THEN N'no address line'
            WHEN NULLIF(LTRIM(RTRIM(o.[City])), '') IS NULL THEN N'no city'
            ELSE N'country not recognised (state / PIN / city did not match)' END AS [Reason]
FROM [Admissions] o
WHERE (NULLIF(LTRIM(RTRIM(o.[AddressLine])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[City])), '') IS NOT NULL
   OR NULLIF(LTRIM(RTRIM(o.[State])), '') IS NOT NULL OR NULLIF(LTRIM(RTRIM(o.[Pincode])), '') IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM [Addresses] a WHERE a.[AdmissionId] = o.[Id])
ORDER BY [Owner], [Id];

-- 3. Copied rows whose text differs from the old columns. Expect no rows (an edit made in the
--    new form after the migration is the only legitimate reason).
SELECT N'Students' AS [Owner], o.[Id], a.[Id] AS [AddressId]
FROM [Students] o
JOIN [Addresses] a ON a.[StudentId] = o.[Id]
WHERE a.[Line1] <> LTRIM(RTRIM(o.[AddressLine]))
   OR a.[City] <> LTRIM(RTRIM(o.[City]))
   OR ISNULL(a.[PostalCode], N'') <> ISNULL(NULLIF(LTRIM(RTRIM(o.[Pincode])), N''), N'')
UNION ALL
SELECT N'Parents' AS [Owner], o.[Id], a.[Id] AS [AddressId]
FROM [Parents] o
JOIN [Addresses] a ON a.[ParentId] = o.[Id]
WHERE a.[Line1] <> LTRIM(RTRIM(o.[AddressLine]))
   OR a.[City] <> LTRIM(RTRIM(o.[City]))
   OR ISNULL(a.[PostalCode], N'') <> ISNULL(NULLIF(LTRIM(RTRIM(o.[Pincode])), N''), N'')
UNION ALL
SELECT N'Admissions' AS [Owner], o.[Id], a.[Id] AS [AddressId]
FROM [Admissions] o
JOIN [Addresses] a ON a.[AdmissionId] = o.[Id]
WHERE a.[Line1] <> LTRIM(RTRIM(o.[AddressLine]))
   OR a.[City] <> LTRIM(RTRIM(o.[City]))
   OR ISNULL(a.[PostalCode], N'') <> ISNULL(NULLIF(LTRIM(RTRIM(o.[Pincode])), N''), N'');

-- 4. Extra safety: every owner has at most one primary and at most one address per type
--    (the unique indexes guarantee it; this shows it). Expect no rows.
SELECT [StudentId], [ParentId], [AdmissionId], COUNT(*) AS [Primaries]
FROM [Addresses]
WHERE [IsPrimary] = 1
GROUP BY [StudentId], [ParentId], [AdmissionId]
HAVING COUNT(*) > 1;
