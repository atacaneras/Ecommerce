import React, { useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { LogIn, UserPlus, Lock, Mail, Loader, Eye, EyeOff, AlertCircle } from 'lucide-react';

export default function LoginPage({ onLoginSuccess }) {
  const { login, register, loading, error } = useAuth();
  const [isLoginMode, setIsLoginMode] = useState(true);
  const [showPassword, setShowPassword] = useState(false);
  const [formData, setFormData] = useState({
    usernameOrEmail: '',
    username: '',
    email: '',
    password: '',
    firstName: '',
    lastName: '',
    phoneNumber: ''
  });
  const [validationErrors, setValidationErrors] = useState({});

  const validatePassword = (password) => {
    const errors = [];
    if (password.length < 8) errors.push('En az 8 karakter olmalı');
    if (!/[A-Z]/.test(password)) errors.push('En az 1 büyük harf içermeli');
    if (!/[a-z]/.test(password)) errors.push('En az 1 küçük harf içermeli');
    if (!/[0-9]/.test(password)) errors.push('En az 1 rakam içermeli');
    if (!/[@$!%*?&]/.test(password)) errors.push('En az 1 özel karakter içermeli (@$!%*?&)');
    return errors;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setValidationErrors({});

    if (isLoginMode) {
      // Login
      if (!formData.usernameOrEmail || !formData.password) {
        setValidationErrors({ general: 'Lütfen tüm alanları doldurun' });
        return;
      }

      const result = await login(formData.usernameOrEmail, formData.password);
      if (result.success) {
        onLoginSuccess();
      }
    } else {
      // Register
      const errors = {};
      
      if (!formData.username) errors.username = 'Kullanıcı adı gerekli';
      if (formData.username && formData.username.length < 3) {
        errors.username = 'Kullanıcı adı en az 3 karakter olmalı';
      }
      
      if (!formData.email) errors.email = 'E-posta gerekli';
      if (formData.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
        errors.email = 'Geçerli bir e-posta adresi girin';
      }
      
      if (!formData.password) errors.password = 'Şifre gerekli';
      const passwordErrors = validatePassword(formData.password);
      if (passwordErrors.length > 0) {
        errors.password = passwordErrors.join(', ');
      }

      if (Object.keys(errors).length > 0) {
        setValidationErrors(errors);
        return;
      }

      const result = await register({
        username: formData.username,
        email: formData.email,
        password: formData.password,
        firstName: formData.firstName,
        lastName: formData.lastName,
        phoneNumber: formData.phoneNumber
      });

      if (result.success) {
        onLoginSuccess();
      }
    }
  };

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
    
    if (validationErrors[name]) {
      setValidationErrors(prev => {
        const newErrors = { ...prev };
        delete newErrors[name];
        return newErrors;
      });
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Logo/Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-gradient-to-br from-blue-500 to-purple-600 rounded-2xl mb-4">
            <Lock className="w-8 h-8 text-white" />
          </div>
          <h1 className="text-3xl font-bold text-white mb-2">E-Ticaret Dashboard</h1>
          <p className="text-slate-400">
            {isLoginMode ? 'Hesabınıza giriş yapın' : 'Yeni hesap oluşturun'}
          </p>
        </div>

        {/* Main Card */}
        <div className="bg-slate-800 rounded-2xl shadow-2xl border border-slate-700 p-8">
          {/* Toggle Buttons */}
          <div className="flex gap-2 mb-6 bg-slate-900 rounded-lg p-1">
            <button
              onClick={() => setIsLoginMode(true)}
              className={`flex-1 py-2 px-4 rounded-md font-medium transition-all ${
                isLoginMode
                  ? 'bg-gradient-to-r from-blue-600 to-purple-600 text-white'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <LogIn className="w-4 h-4 inline mr-2" />
              Giriş Yap
            </button>
            <button
              onClick={() => setIsLoginMode(false)}
              className={`flex-1 py-2 px-4 rounded-md font-medium transition-all ${
                !isLoginMode
                  ? 'bg-gradient-to-r from-blue-600 to-purple-600 text-white'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              <UserPlus className="w-4 h-4 inline mr-2" />
              Kayıt Ol
            </button>
          </div>

          {/* Error Message */}
          {(error || validationErrors.general) && (
            <div className="mb-4 p-3 bg-red-500/10 border border-red-500/30 rounded-lg flex items-start gap-2">
              <AlertCircle className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
              <span className="text-red-300 text-sm">
                {error || validationErrors.general}
              </span>
            </div>
          )}

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-4">
            {isLoginMode ? (
              <>
                {/* Login Form */}
                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    Kullanıcı Adı veya E-posta
                  </label>
                  <div className="relative">
                    <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" />
                    <input
                      type="text"
                      name="usernameOrEmail"
                      value={formData.usernameOrEmail}
                      onChange={handleChange}
                      className="w-full bg-slate-700 border border-slate-600 rounded-lg pl-10 pr-4 py-3 text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                      placeholder="admin veya admin@beymen.com"
                      required
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    Şifre
                  </label>
                  <div className="relative">
                    <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" />
                    <input
                      type={showPassword ? 'text' : 'password'}
                      name="password"
                      value={formData.password}
                      onChange={handleChange}
                      className="w-full bg-slate-700 border border-slate-600 rounded-lg pl-10 pr-12 py-3 text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                      placeholder="••••••••"
                      required
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white"
                    >
                      {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                    </button>
                  </div>
                </div>
              </>
            ) : (
              <>
                {/* Register Form */}
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-slate-300 mb-2">
                      Ad
                    </label>
                    <input
                      type="text"
                      name="firstName"
                      value={formData.firstName}
                      onChange={handleChange}
                      className="w-full bg-slate-700 border border-slate-600 rounded-lg px-4 py-3 text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
                      placeholder="Ahmet"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-slate-300 mb-2">
                      Soyad
                    </label>
                    <input
                      type="text"
                      name="lastName"
                      value={formData.lastName}
                      onChange={handleChange}
                      className="w-full bg-slate-700 border border-slate-600 rounded-lg px-4 py-3 text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
                      placeholder="Yılmaz"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    Kullanıcı Adı *
                  </label>
                  <input
                    type="text"
                    name="username"
                    value={formData.username}
                    onChange={handleChange}
                    className={`w-full bg-slate-700 border rounded-lg px-4 py-3 text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                      validationErrors.username ? 'border-red-500' : 'border-slate-600'
                    }`}
                    placeholder="ahmetyilmaz"
                    required
                  />
                  {validationErrors.username && (
                    <p className="text-red-400 text-xs mt-1">{validationErrors.username}</p>
                  )}
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    E-posta *
                  </label>
                  <input
                    type="email"
                    name="email"
                    value={formData.email}
                    onChange={handleChange}
                    className={`w-full bg-slate-700 border rounded-lg px-4 py-3 text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                      validationErrors.email ? 'border-red-500' : 'border-slate-600'
                    }`}
                    placeholder="ahmet@example.com"
                    required
                  />
                  {validationErrors.email && (
                    <p className="text-red-400 text-xs mt-1">{validationErrors.email}</p>
                  )}
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    Telefon
                  </label>
                  <input
                    type="tel"
                    name="phoneNumber"
                    value={formData.phoneNumber}
                    onChange={handleChange}
                    className="w-full bg-slate-700 border border-slate-600 rounded-lg px-4 py-3 text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    placeholder="+905551234567"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-300 mb-2">
                    Şifre *
                  </label>
                  <div className="relative">
                    <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" />
                    <input
                      type={showPassword ? 'text' : 'password'}
                      name="password"
                      value={formData.password}
                      onChange={handleChange}
                      className={`w-full bg-slate-700 border rounded-lg pl-10 pr-12 py-3 text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 ${
                        validationErrors.password ? 'border-red-500' : 'border-slate-600'
                      }`}
                      placeholder="••••••••"
                      required
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-white"
                    >
                      {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                    </button>
                  </div>
                  {validationErrors.password && (
                    <p className="text-red-400 text-xs mt-1">{validationErrors.password}</p>
                  )}
                  {!validationErrors.password && (
                    <p className="text-slate-400 text-xs mt-1">
                      Min 8 karakter, 1 büyük harf, 1 küçük harf, 1 rakam, 1 özel karakter
                    </p>
                  )}
                </div>
              </>
            )}

            {/* Submit Button */}
            <button
              type="submit"
              disabled={loading}
              className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-bold py-3 rounded-lg transition-all flex items-center justify-center gap-2 mt-6"
            >
              {loading ? (
                <>
                  <Loader className="w-5 h-5 animate-spin" />
                  İşleniyor...
                </>
              ) : (
                <>
                  {isLoginMode ? <LogIn className="w-5 h-5" /> : <UserPlus className="w-5 h-5" />}
                  {isLoginMode ? 'Giriş Yap' : 'Hesap Oluştur'}
                </>
              )}
            </button>
          </form>

          {/* Demo Credentials */}
          {isLoginMode && (
            <div className="mt-6 p-4 bg-blue-500/10 border border-blue-500/30 rounded-lg">
              <p className="text-blue-300 text-sm font-medium mb-2">🔑 Demo Hesap:</p>
              <p className="text-blue-200 text-xs">
                Kullanıcı: <code className="bg-slate-900 px-2 py-0.5 rounded">admin</code>
              </p>
              <p className="text-blue-200 text-xs mt-1">
                Şifre: <code className="bg-slate-900 px-2 py-0.5 rounded">Admin@123</code>
              </p>
            </div>
          )}
        </div>

        {/* Footer */}
        <p className="text-center text-slate-400 text-sm mt-6">
          Güvenli giriş sistemi ile korunuyorsunuz 🔒
        </p>
      </div>
    </div>
  );
}