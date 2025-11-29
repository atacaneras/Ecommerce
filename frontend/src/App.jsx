import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext'; 
import Dashboard from './components/Dashboard';
import LoginPage from './components/auth/LoginPage';
import './App.css';

const ProtectedRoute = ({ children }) => {
  const { user, loading } = useAuth();

  if (loading) {
    return <div className="flex items-center justify-center min-h-screen text-white">Yükleniyor...</div>;
  }

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  return children;
};

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          {/* Login Sayfası */}
          <Route path="/login" element={<LoginPage onLoginSuccess={() => window.location.href = '/'} />} />

          {/* Ana Sayfa (Dashboard) - Sadece giriş yapmışlar görebilir */}
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <div className="app-container">
                  <Dashboard />
                </div>
              </ProtectedRoute>
            }
          />

          {/* Bilinmeyen rotalar ana sayfaya gitsin */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;