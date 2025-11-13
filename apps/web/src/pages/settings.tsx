import { useEffect, useState } from "react";
import { useRouter } from "next/router";
import { QRCodeSVG } from "qrcode.react";
import { ApiError, api, type TotpSetupResponse, type TwoFactorStatusResponse, type UserProfile } from "@/lib/api";

export default function SettingsPage() {
  const router = useRouter();
  const [status, setStatus] = useState<TwoFactorStatusResponse | null>(null);
  const [twoFactorLoading, setTwoFactorLoading] = useState(false);
  const [twoFactorError, setTwoFactorError] = useState<string | null>(null);
  const [initialLoading, setInitialLoading] = useState(true);

  // Profile state
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [profileForm, setProfileForm] = useState({ displayName: "", email: "" });
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileSuccess, setProfileSuccess] = useState<string | null>(null);
  const [profileError, setProfileError] = useState<string | null>(null);

  // Password change state
  const [passwordForm, setPasswordForm] = useState({
    currentPassword: "",
    newPassword: "",
    confirmPassword: ""
  });
  const [passwordSaving, setPasswordSaving] = useState(false);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [passwordSuccess, setPasswordSuccess] = useState<string | null>(null);

  // 2FA Setup state
  const [setup, setSetup] = useState<TotpSetupResponse | null>(null);
  const [verificationCode, setVerificationCode] = useState("");
  const [showBackupCodes, setShowBackupCodes] = useState(false);

  // 2FA Disable state
  const [disablePassword, setDisablePassword] = useState("");
  const [disableCode, setDisableCode] = useState("");

  useEffect(() => {
    const bootstrap = async () => {
      setInitialLoading(true);
      await Promise.allSettled([loadStatus(), loadProfile()]);
      setInitialLoading(false);
    };

    void bootstrap();
  }, []);

  const loadStatus = async () => {
    try {
      const statusResponse = await api.twoFactor.getStatus();
      setStatus(statusResponse);
      setTwoFactorError(null);
    } catch (err) {
      console.error("Failed to load 2FA status:", err);
      setTwoFactorError("Failed to load 2FA status");
    }
  };

  const loadProfile = async () => {
    try {
      const profileResponse = await api.profile.get();
      if (profileResponse) {
        setProfile(profileResponse);
        setProfileForm({
          displayName: profileResponse.displayName ?? "",
          email: profileResponse.email ?? ""
        });
      }
      setProfileError(null);
    } catch (err) {
      console.error("Failed to load profile:", err);
      setProfileError("Failed to load profile");
    }
  };

  const handleUpdateProfile = async () => {
    setProfileSuccess(null);
    setProfileError(null);

    if (!profile) {
      setProfileError("Profile data is still loading. Please try again in a moment.");
      return;
    }

    const displayName = (profileForm.displayName ?? "").trim();
    const email = (profileForm.email ?? "").trim();

    if (!displayName) {
      setProfileError("Display name cannot be empty.");
      return;
    }

    if (displayName === profile.displayName && email === profile.email) {
      setProfileError("Update at least one field before saving.");
      return;
    }

    try {
      setProfileSaving(true);
      await api.profile.update({
        displayName: displayName || undefined,
        email: email || undefined
      });
      setProfileSuccess("Profile updated successfully.");
      await loadProfile();
    } catch (err) {
      console.error("Failed to update profile:", err);
      const message = err instanceof ApiError ? err.message : "Failed to update profile.";
      setProfileError(message);
    } finally {
      setProfileSaving(false);
    }
  };

  const handleChangePassword = async () => {
    setPasswordError(null);
    setPasswordSuccess(null);

    if (!passwordForm.currentPassword.trim() || !passwordForm.newPassword.trim()) {
      setPasswordError("Provide both your current and new password.");
      return;
    }

    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setPasswordError("New password and confirmation do not match.");
      return;
    }

    try {
      setPasswordSaving(true);
      await api.profile.changePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword
      });
      setPasswordSuccess("Password changed successfully.");
      setPasswordForm({ currentPassword: "", newPassword: "", confirmPassword: "" });
    } catch (err) {
      console.error("Failed to change password:", err);
      const message = err instanceof ApiError ? err.message : "Failed to change password.";
      setPasswordError(message);
    } finally {
      setPasswordSaving(false);
    }
  };

  const handleSetup = async () => {
    try {
      setTwoFactorLoading(true);
      const setupResponse = await api.twoFactor.setup();
      setSetup(setupResponse);
      setShowBackupCodes(true);
      setTwoFactorError(null);
    } catch (err) {
      console.error("Failed to setup 2FA:", err);
      setTwoFactorError("Failed to setup 2FA");
    } finally {
      setTwoFactorLoading(false);
    }
  };

  const handleEnable = async () => {
    try {
      setTwoFactorLoading(true);
      await api.twoFactor.enable(verificationCode);
      alert("Two-factor authentication enabled successfully!");
      setSetup(null);
      setShowBackupCodes(false);
      setVerificationCode("");
      await loadStatus();
    } catch (err) {
      console.error("Failed to enable 2FA:", err);
      setTwoFactorError("Invalid verification code. Please try again.");
    } finally {
      setTwoFactorLoading(false);
    }
  };

  const handleDisable = async () => {
    if (!confirm("Are you sure you want to disable 2FA? Your account will be less secure.")) {
      return;
    }

    try {
      setTwoFactorLoading(true);
      await api.twoFactor.disable(disablePassword, disableCode);
      alert("Two-factor authentication disabled.");
      setDisablePassword("");
      setDisableCode("");
      await loadStatus();
    } catch (err) {
      console.error("Failed to disable 2FA:", err);
      setTwoFactorError("Failed to disable 2FA. Check your password and code.");
    } finally {
      setTwoFactorLoading(false);
    }
  };

  const downloadBackupCodes = () => {
    if (!setup?.backupCodes) return;

    const text = `MeepleAI Backup Codes\n\n${setup.backupCodes.join("\n")}\n\nKeep these codes in a secure location. Each code can only be used once.`;
    const blob = new Blob([text], { type: "text/plain" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "meepleai-backup-codes.txt";
    a.click();
    URL.revokeObjectURL(url);
  };

  if (initialLoading) {
    return (
      <div className="min-h-screen bg-gray-50 p-8">
        <div className="max-w-3xl mx-auto">
          <p>Loading...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 p-8">
      <div className="max-w-3xl mx-auto">
        <div className="bg-white rounded-lg shadow p-6">
          <h1 className="text-2xl font-bold mb-6">Account Settings</h1>

          {twoFactorError && (
            <div className="bg-red-50 border border-red-200 text-red-800 px-4 py-3 rounded mb-6">
              {twoFactorError}
            </div>
          )}

          <section className="mb-8">
            <h2 className="text-xl font-semibold mb-4">Profile Information</h2>
            {profileSuccess && (
              <div className="bg-green-50 border border-green-200 text-green-800 px-4 py-3 rounded mb-4">
                {profileSuccess}
              </div>
            )}
            {profileError && (
              <div className="bg-red-50 border border-red-200 text-red-800 px-4 py-3 rounded mb-4">
                {profileError}
              </div>
            )}
            {profile ? (
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Display Name</label>
                  <input
                    type="text"
                    value={profileForm.displayName}
                    onChange={(e) => setProfileForm((prev) => ({ ...prev, displayName: e.target.value }))}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Email</label>
                  <input
                    type="email"
                    value={profileForm.email}
                    onChange={(e) => setProfileForm((prev) => ({ ...prev, email: e.target.value }))}
                    className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </div>
                <div className="text-sm text-gray-500">
                  <p>
                    Role: <span className="font-medium capitalize">{profile.role.toLowerCase()}</span>
                  </p>
                  <p>Member since {new Date(profile.createdAt).toLocaleDateString()}</p>
                </div>
                <button
                  onClick={handleUpdateProfile}
                  disabled={!profile || profileSaving}
                  className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
                >
                  {profileSaving ? "Saving..." : "Save Changes"}
                </button>
              </div>
            ) : (
              <p className="text-sm text-gray-600">Profile details are unavailable right now.</p>
            )}
          </section>

          <section className="mb-8 border-t border-gray-200 pt-6">
            <h2 className="text-xl font-semibold mb-4">Change Password</h2>
            {passwordSuccess && (
              <div className="bg-green-50 border border-green-200 text-green-800 px-4 py-3 rounded mb-4">
                {passwordSuccess}
              </div>
            )}
            {passwordError && (
              <div className="bg-red-50 border border-red-200 text-red-800 px-4 py-3 rounded mb-4">
                {passwordError}
              </div>
            )}
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Current Password</label>
                <input
                  type="password"
                  value={passwordForm.currentPassword}
                  onChange={(e) => setPasswordForm((prev) => ({ ...prev, currentPassword: e.target.value }))}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">New Password</label>
                <input
                  type="password"
                  value={passwordForm.newPassword}
                  onChange={(e) => setPasswordForm((prev) => ({ ...prev, newPassword: e.target.value }))}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Confirm New Password</label>
                <input
                  type="password"
                  value={passwordForm.confirmPassword}
                  onChange={(e) => setPasswordForm((prev) => ({ ...prev, confirmPassword: e.target.value }))}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <button
                onClick={handleChangePassword}
                disabled={passwordSaving}
                className="px-4 py-2 bg-gray-900 text-white rounded-md hover:bg-gray-800 disabled:opacity-50"
              >
                {passwordSaving ? "Updating..." : "Change Password"}
              </button>
            </div>
          </section>

          <section className="mb-8 border-t border-gray-200 pt-6">
            <h2 className="text-xl font-semibold mb-4">Two-Factor Authentication</h2>

            {status?.isTwoFactorEnabled ? (
              <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                <div className="flex items-center mb-2">
                  <svg className="w-5 h-5 text-green-600 mr-2" fill="currentColor" viewBox="0 0 20 20">
                    <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                  </svg>
                  <p className="text-green-800 font-medium">Two-factor authentication is enabled</p>
                </div>
                <p className="text-sm text-green-700 mb-4">
                  Backup codes remaining: {status.backupCodesCount}
                </p>

                {status.backupCodesCount < 3 && (
                  <div className="bg-yellow-50 border border-yellow-200 rounded p-3 mb-4">
                    <p className="text-sm text-yellow-800">
                      Warning: You have only {status.backupCodesCount} backup codes remaining.
                      Consider disabling and re-enabling 2FA to generate new backup codes.
                    </p>
                  </div>
                )}

                <div className="mt-6 pt-6 border-t border-green-200">
                  <h3 className="font-medium text-gray-900 mb-4">Disable Two-Factor Authentication</h3>
                  <div className="space-y-3 max-w-md">
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-1">
                        Current Password
                      </label>
                      <input
                        type="password"
                        placeholder="Enter your password"
                        value={disablePassword}
                        onChange={(e) => setDisablePassword(e.target.value)}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                      />
                    </div>
                    <div>
                      <label className="block text-sm font-medium text-gray-700 mb-1">
                        TOTP Code or Backup Code
                      </label>
                      <input
                        type="text"
                        placeholder="000000 or XXXX-XXXX"
                        value={disableCode}
                        onChange={(e) => setDisableCode(e.target.value)}
                        className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                      />
                    </div>
                    <button
                      onClick={handleDisable}
                      disabled={twoFactorLoading || !disablePassword || !disableCode}
                      className="px-4 py-2 bg-red-600 text-white rounded-md hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      {twoFactorLoading ? "Disabling..." : "Disable 2FA"}
                    </button>
                  </div>
                </div>
              </div>
            ) : (
              <div>
                {!setup ? (
                  <div>
                    <p className="text-gray-600 mb-4">
                      Two-factor authentication adds an extra layer of security to your account.
                      You&apos;ll need your password and a code from your authenticator app to log in.
                    </p>
                    <button
                      onClick={handleSetup}
                      disabled={twoFactorLoading}
                      className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
                    >
                      {twoFactorLoading ? "Setting up..." : "Enable Two-Factor Authentication"}
                    </button>
                  </div>
                ) : (
                  <div className="space-y-6">
                    <div className="border border-gray-200 rounded-lg p-4">
                      <h3 className="font-medium text-gray-900 mb-2">Step 1: Scan QR Code</h3>
                      <p className="text-sm text-gray-600 mb-4">
                        Scan this QR code with your authenticator app (Google Authenticator, Authy, Microsoft Authenticator, etc.)
                      </p>
                      <div className="flex justify-center bg-white p-4 rounded border border-gray-300">
                        <QRCodeSVG value={setup.qrCodeUri} size={256} />
                      </div>
                      <details className="mt-4">
                        <summary className="text-sm text-gray-600 cursor-pointer hover:text-gray-900">
                          Can&apos;t scan QR code? Enter manually
                        </summary>
                        <div className="mt-2 p-3 bg-gray-100 rounded">
                          <p className="text-xs text-gray-600 mb-1">Manual Entry Key:</p>
                          <code className="text-sm font-mono bg-white px-2 py-1 rounded border border-gray-300">
                            {setup.secret}
                          </code>
                        </div>
                      </details>
                    </div>

                    {showBackupCodes && (
                      <div className="bg-yellow-50 border border-yellow-300 rounded-lg p-4">
                        <h3 className="font-medium text-yellow-900 mb-2">Step 2: Save Backup Codes</h3>
                        <p className="text-sm text-yellow-800 mb-3">
                          Important: Save these codes in a secure location. Each code can only be used once.
                          You won&apos;t be able to see them again!
                        </p>
                        <div className="grid grid-cols-2 gap-2 mb-4">
                          {setup.backupCodes.map((code, i) => (
                            <div
                              key={i}
                              className="bg-white px-3 py-2 rounded font-mono text-sm text-center border border-yellow-200"
                            >
                              {code}
                            </div>
                          ))}
                        </div>
                        <div className="flex gap-2">
                          <button
                            onClick={downloadBackupCodes}
                            className="px-4 py-2 bg-yellow-600 text-white rounded-md hover:bg-yellow-700 text-sm"
                          >
                            Download Codes
                          </button>
                          <button
                            onClick={() => setShowBackupCodes(false)}
                            className="px-4 py-2 bg-yellow-800 text-white rounded-md hover:bg-yellow-900 text-sm"
                          >
                            I&apos;ve Saved My Codes
                          </button>
                        </div>
                      </div>
                    )}

                    {!showBackupCodes && (
                      <div className="border border-gray-200 rounded-lg p-4">
                        <h3 className="font-medium text-gray-900 mb-2">Step 3: Verify & Enable</h3>
                        <p className="text-sm text-gray-600 mb-3">
                          Enter the 6-digit code from your authenticator app to confirm setup
                        </p>
                        <div className="flex items-center gap-2">
                          <input
                            type="text"
                            placeholder="000000"
                            value={verificationCode}
                            onChange={(e) => setVerificationCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
                            maxLength={6}
                            className="w-32 px-3 py-2 border border-gray-300 rounded-md text-center font-mono text-lg focus:outline-none focus:ring-2 focus:ring-green-500"
                          />
                          <button
                            onClick={handleEnable}
                            disabled={twoFactorLoading || verificationCode.length !== 6}
                            className="px-4 py-2 bg-green-600 text-white rounded-md hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed"
                          >
                            {twoFactorLoading ? "Verifying..." : "Verify & Enable"}
                          </button>
                        </div>
                      </div>
                    )}

                    <button
                      onClick={() => {
                        setSetup(null);
                        setShowBackupCodes(false);
                        setVerificationCode("");
                      }}
                      className="text-sm text-gray-600 hover:text-gray-900"
                    >
                      Cancel Setup
                    </button>
                  </div>
                )}
              </div>
            )}
          </section>

          <div className="pt-6 border-t border-gray-200">
            <button
              onClick={() => router.push("/")}
              className="text-blue-600 hover:text-blue-800"
            >
              Back to Home
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
