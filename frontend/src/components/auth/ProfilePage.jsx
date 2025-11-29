import React from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { User, Mail, Phone, Shield, ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

export default function ProfilePage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  if (!user) return null;

  const getInitials = () => {
    if (user.firstName && user.lastName) {
      return `${user.firstName[0]}${user.lastName[0]}`.toUpperCase();
    }
    return user.username.substring(0, 2).toUpperCase();
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 p-6">
      <div className="max-w-2xl mx-auto">
        {/* Geri Dön Butonu */}
        <button 
          onClick={() => navigate('/')}
          className="mb-6 flex items-center gap-2 text-slate-400 hover:text-white transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
          Ana Sayfaya Dön
        </button>

        <div className="bg-slate-800 rounded-2xl border border-slate-700 shadow-2xl overflow-hidden">
          {/* Üst Başlık Kısmı */}
          <div className="bg-slate-900/50 p-8 text-center border-b border-slate-700">
            <div className="w-24 h-24 mx-auto bg-gradient-to-br from-blue-500 to-purple-600 rounded-full flex items-center justify-center text-white text-3xl font-bold shadow-lg mb-4">
              {getInitials()}
            </div>
            <h1 className="text-2xl font-bold text-white">
              {user.firstName} {user.lastName}
            </h1>
            <p className="text-blue-400 font-medium">@{user.username}</p>
          </div>

          {/* Bilgiler */}
          <div className="p-8 space-y-6">
            <div className="grid md:grid-cols-2 gap-6">
              {/* E-posta */}
              <div className="bg-slate-700/30 p-4 rounded-xl border border-slate-700">
                <div className="flex items-center gap-3 mb-2">
                  <div className="p-2 bg-blue-500/20 rounded-lg">
                    <Mail className="w-5 h-5 text-blue-400" />
                  </div>
                  <span className="text-slate-400 text-sm font-medium">E-posta Adresi</span>
                </div>
                <p className="text-white font-medium pl-12">{user.email}</p>
              </div>

              {/* Telefon */}
              <div className="bg-slate-700/30 p-4 rounded-xl border border-slate-700">
                <div className="flex items-center gap-3 mb-2">
                  <div className="p-2 bg-green-500/20 rounded-lg">
                    <Phone className="w-5 h-5 text-green-400" />
                  </div>
                  <span className="text-slate-400 text-sm font-medium">Telefon</span>
                </div>
                <p className="text-white font-medium pl-12">
                  {user.phoneNumber || 'Belirtilmemiş'}
                </p>
              </div>

              {/* Rol */}
              <div className="bg-slate-700/30 p-4 rounded-xl border border-slate-700">
                <div className="flex items-center gap-3 mb-2">
                  <div className="p-2 bg-purple-500/20 rounded-lg">
                    <Shield className="w-5 h-5 text-purple-400" />
                  </div>
                  <span className="text-slate-400 text-sm font-medium">Kullanıcı Rolü</span>
                </div>
                <p className="text-white font-medium pl-12">{user.role}</p>
              </div>

              {/* Kullanıcı ID */}
              <div className="bg-slate-700/30 p-4 rounded-xl border border-slate-700">
                <div className="flex items-center gap-3 mb-2">
                  <div className="p-2 bg-orange-500/20 rounded-lg">
                    <User className="w-5 h-5 text-orange-400" />
                  </div>
                  <span className="text-slate-400 text-sm font-medium">Kullanıcı ID</span>
                </div>
                <p className="text-white font-medium pl-12 font-mono text-sm">#{user.id}</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}