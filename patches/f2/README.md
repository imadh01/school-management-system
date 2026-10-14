# F2 patch (school time zone)

Do NOT commit this folder. Steps (after committing G2 and everything pending):

1. `git checkout -b feature/f2-school-timezone`
2. Copy everything under `patches/f2/files/` over the repository root (same relative paths).
   (`f2.patch` is the same change as a diff, made against the state after G2: `git apply --check patches/f2/f2.patch` to try it.)
3. Delete the `patches` folder.
4. Run the tests, then commit. See `docs/f2-school-time-zone.md`.
