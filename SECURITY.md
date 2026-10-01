# Security scope and known limitations

This is an academic prototype for source review and local experimentation with synthetic data. Do not deploy it with real student, identity, financial, or authentication data without a separate security review.

## Secret and data handling

- Configure SMTP, reCAPTCHA, database connections, and encryption keys outside source control.
- Keep database files, uploads, logs, screenshots, and configuration backups private. The repository deliberately excludes the original database and academic report.
- If a previous credential-bearing copy was ever shared, revoke the email app password and rotate the reCAPTCHA secret. Rotate encryption keys only with a planned data migration and a private backup of the existing key.
- Never include credentials, identity numbers, password hashes, or personal records in issues or pull requests.

## Known implementation limitations

The publication review identified these areas for further work; this is not an exhaustive vulnerability assessment:

1. **Encryption format:** `SecurityHelper` retains the academic AES-CBC implementation with a fixed zero IV and no authentication tag. Moving the key out of the repository does not fix that design. Replace it with a vetted authenticated-encryption design, unique nonces, versioned ciphertext, and a tested migration before using real data.
2. **CAPTCHA state:** student login binds `IsCaptchaVerified` from the request. CAPTCHA success must be bound to trusted server-side state and validated across the complete login flow.
3. **Authentication and recovery:** MFA, reset-token expiry, replay resistance, resend limits, lockouts, and first-login password changes need systematic tests. In-memory attempt tracking is unsuitable for consistent enforcement across multiple processes or restarts.
4. **Network-derived identity:** some login paths read forwarded headers directly. Trust forwarded addresses only from explicitly configured proxies. IP address and User-Agent strings are not strong device identity.
5. **External IP geolocation:** the academic code calls an HTTP geolocation service with client IP addresses. Disable or replace this integration and assess privacy requirements before deployment.
6. **Authorization and output handling:** review resource ownership checks and HTML encoding in every page and email template, in addition to folder-level role policies. Review uploads and cookie settings in the same deployment assessment.
7. **Operational controls:** dependency review, automated security tests, backup/restore testing, key management, and monitoring remain deployment work. A successful build or secret scan does not prove the application secure.

## Reporting

For a non-sensitive defect, open an issue describing the affected module and reproduction using synthetic data. Do not post secrets, personal data, or details from a live deployment in public issues.
