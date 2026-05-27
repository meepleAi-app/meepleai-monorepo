import { TOTP, Secret } from 'otpauth';

/**
 * Mint a current 6-digit TOTP code from a Base32 secret — mirrors the backend
 * (OtpNet `new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp()`), so an e2e
 * can complete a real 2FA enrollment against the live API. RFC 6238 defaults:
 * SHA1, 6 digits, 30s period.
 */
export function mintTotp(base32Secret: string): string {
  const totp = new TOTP({
    secret: Secret.fromBase32(base32Secret.replace(/\s/g, '')),
    digits: 6,
    period: 30,
    algorithm: 'SHA1',
  });
  return totp.generate();
}
