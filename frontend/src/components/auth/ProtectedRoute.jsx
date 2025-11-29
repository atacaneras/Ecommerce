import React from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { Shield, AlertCircle } from 'lucide-react';

export default function ProtectedRoute({ children, requireAdmin = false }) {
  const { isAuthenticated, isAdmin, loading } = useAuth();

  if (loading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 flex items-center justify-center">
        <div className="text-center">
          <div className="inline-block animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500 mb-4"></div>
          <p className="text-slate-400">Yükleniyor...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null; // Will be handled by App.jsx
  }

  if (requireAdmin && !isAdmin) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 flex items-center justify-center p-4">
        <div className="max-w-md w-full">
          <div className="bg-slate-800 rounded-2xl shadow-2xl border border-red-500/30 p-8 text-center">
            <div className="inline-flex items-center justify-center w-16 h-16 bg-red-500/20 rounded-full mb-4">
              <AlertCircle className="w-8 h-8 text-red-400" />
            </div>
            <h2 className="text-2xl font-bold text-white mb-2">Erişim Engellendi</h2>
            <p className="text-slate-400 mb-6">
              Bu sayfaya erişim için admin yetkisi gereklidir.
            </p>
            <div className="flex items-center justify-center gap-2 text-slate-500 text-sm">
              <Shield className="w-4 h-4" />
              <span>Admin Yetki Seviyesi: {isAdmin ? '✓' : '✗'}</span>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}