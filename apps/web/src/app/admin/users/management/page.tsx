import { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'User Management | Admin | MeepleAI',
  description: 'Manage platform users and permissions',
};

export default function UserManagementPage() {
  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-amber-50/30 to-orange-50/20 p-8">
      <div className="max-w-[1600px] mx-auto">
        <div className="mb-8">
          <h1 className="font-quicksand font-bold text-4xl text-slate-900 mb-2">
            User Management
          </h1>
          <p className="font-nunito text-lg text-slate-600">
            Comprehensive user administration and account management
          </p>
        </div>

        {/* TODO: Implement full user management table */}
        <div className="bg-white/70 backdrop-blur-md rounded-xl border border-amber-200/60 p-12 text-center">
          <p className="font-nunito text-slate-500">
            Full user management page - Implementation pending (Issue #4196)
          </p>
          <p className="font-nunito text-sm text-slate-400 mt-2">
            Will include: Paginated table, advanced filters, bulk operations, activity timeline, export
          </p>
        </div>
      </div>
    </div>
  );
}
