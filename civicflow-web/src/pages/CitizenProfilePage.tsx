import { useState, useEffect, type FormEvent } from "react";
import {
  CitizenApiError,
  getMyCitizenProfile,
  saveMyCitizenProfile,
  type CitizenProfile,
} from "../api/citizens";

export default function CitizenProfilePage({
  onSignIn,
}: {
  onSignIn?: () => void;
} = {}) {
  const initialForm = {
    fullName: "",
    email: "",
    phoneNumber: "",
    nidNumber: "",
    citizenshipNumber: "",
    age: "",
    gender: "",
    permanentAddress: "",
    temporaryAddress: "",
  };

  const [profile, setProfile] = useState<CitizenProfile | null>(null);
  const [form, setForm] = useState(initialForm);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [requiresSignIn, setRequiresSignIn] = useState(false);
  const [forbidden, setForbidden] = useState(false);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setRequiresSignIn(false);
        setForbidden(false);
        const data = await getMyCitizenProfile();
        if (data) {
          setProfile(data);
          setForm({
            fullName: data.fullName ?? "",
            email: data.email ?? "",
            phoneNumber: data.phoneNumber ?? "",
            nidNumber: data.nidNumber ?? "",
            citizenshipNumber: data.citizenshipNumber ?? "",
            age: data.age !== null && data.age !== undefined ? String(data.age) : "",
            gender: data.gender ?? "",
            permanentAddress: data.permanentAddress ?? "",
            temporaryAddress: data.temporaryAddress ?? "",
          });
        }
      } catch (err) {
        if (err instanceof CitizenApiError && err.status === 401) {
          setRequiresSignIn(true);
        } else if (err instanceof CitizenApiError && err.status === 403) {
          setForbidden(true);
        } else if (err instanceof Error) {
          setError(err.message);
        } else {
          setError("Failed to load profile");
        }
      } finally {
        setLoading(false);
      }
    };

    void load();
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);
    setSaving(true);

    try {
      const ageValue = form.age.trim() === "" ? null : Number.parseInt(form.age, 10);
      const saved = await saveMyCitizenProfile({
        fullName: form.fullName,
        email: form.email,
        phoneNumber: form.phoneNumber || undefined,
        nidNumber: form.nidNumber || undefined,
        citizenshipNumber: form.citizenshipNumber || undefined,
        age: ageValue,
        gender: form.gender || undefined,
        permanentAddress: form.permanentAddress || undefined,
        temporaryAddress: form.temporaryAddress || undefined,
      });
      setProfile(saved);
      setSuccess(profile ? "Profile updated successfully" : "Profile created successfully");
    } catch (err) {
      if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Failed to save profile");
      }
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="max-w-4xl mx-auto p-8">
        <p>Loading citizen profile...</p>
      </div>
    );
  }

  if (requiresSignIn) {
    return (
      <div className="max-w-4xl mx-auto p-8">
        <h1 className="text-3xl font-bold mb-4">Citizen Profile</h1>
        <p className="mb-6 text-gray-700">
          Sign in with your citizen account to create or update your profile.
        </p>
        <button
          type="button"
          onClick={() => {
            if (onSignIn) {
              onSignIn();
            } else {
              window.location.assign("/citizen/login");
            }
          }}
          className="px-6 py-3 bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 transition-colors"
        >
          Sign in
        </button>
      </div>
    );
  }

  if (forbidden) {
    return (
      <div className="max-w-4xl mx-auto p-8">
        <h1 className="text-3xl font-bold mb-4">Citizen Profile</h1>
        <div className="bg-red-50 border border-red-200 text-red-800 px-4 py-3 rounded">
          Your current session is not authorized for citizen profile access. Sign
          in through the citizen portal to continue.
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto p-8">
      <h1 className="text-3xl font-bold mb-8">Citizen Profile</h1>

      <form onSubmit={handleSubmit} className="space-y-6">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-800 px-4 py-3 rounded">
            {error}
          </div>
        )}
        {success && (
          <div className="bg-green-50 border border-green-200 text-green-800 px-4 py-3 rounded">
            {success}
          </div>
        )}

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <label htmlFor="fullName" className="block text-sm font-medium text-gray-700 mb-2">
              Full Name *
            </label>
            <input
              id="fullName"
              type="text"
              required
              maxLength={150}
              value={form.fullName}
              onChange={(e) => setForm((f) => ({ ...f, fullName: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div>
            <label htmlFor="email" className="block text-sm font-medium text-gray-700 mb-2">
              Email *
            </label>
            <input
              id="email"
              type="email"
              required
              maxLength={254}
              value={form.email}
              onChange={(e) => setForm((f) => ({ ...f, email: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div>
            <label htmlFor="phoneNumber" className="block text-sm font-medium text-gray-700 mb-2">
              Phone Number
            </label>
            <input
              id="phoneNumber"
              type="tel"
              maxLength={30}
              value={form.phoneNumber}
              onChange={(e) => setForm((f) => ({ ...f, phoneNumber: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div>
            <label htmlFor="nidNumber" className="block text-sm font-medium text-gray-700 mb-2">
              NID Number
            </label>
            <input
              id="nidNumber"
              type="text"
              maxLength={50}
              value={form.nidNumber}
              onChange={(e) => setForm((f) => ({ ...f, nidNumber: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div>
            <label htmlFor="citizenshipNumber" className="block text-sm font-medium text-gray-700 mb-2">
              Citizenship Number
            </label>
            <input
              id="citizenshipNumber"
              type="text"
              maxLength={50}
              value={form.citizenshipNumber}
              onChange={(e) => setForm((f) => ({ ...f, citizenshipNumber: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div>
            <label htmlFor="age" className="block text-sm font-medium text-gray-700 mb-2">
              Age
            </label>
            <input
              id="age"
              type="number"
              min={0}
              max={150}
              value={form.age}
              onChange={(e) => setForm((f) => ({ ...f, age: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div>
            <label htmlFor="gender" className="block text-sm font-medium text-gray-700 mb-2">
              Gender
            </label>
            <input
              id="gender"
              type="text"
              maxLength={20}
              value={form.gender}
              onChange={(e) => setForm((f) => ({ ...f, gender: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div>
            <label htmlFor="permanentAddress" className="block text-sm font-medium text-gray-700 mb-2">
              Permanent Address
            </label>
            <textarea
              id="permanentAddress"
              rows={3}
              maxLength={500}
              value={form.permanentAddress}
              onChange={(e) => setForm((f) => ({ ...f, permanentAddress: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          <div>
            <label htmlFor="temporaryAddress" className="block text-sm font-medium text-gray-700 mb-2">
              Temporary Address
            </label>
            <textarea
              id="temporaryAddress"
              rows={3}
              maxLength={500}
              value={form.temporaryAddress}
              onChange={(e) => setForm((f) => ({ ...f, temporaryAddress: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>
        </div>

        <div className="flex justify-end">
          <button
            type="submit"
            disabled={saving}
            className="px-6 py-3 bg-blue-600 text-white font-semibold rounded-lg hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {saving ? "Saving..." : profile ? "Update Profile" : "Create Profile"}
          </button>
        </div>
      </form>
    </div>
  );
}
