import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import Dashboard from './components/Dashboard';
import LoginPage from './components/auth/LoginPage';
import ProfilePage from './components/auth/ProfilePage'; // 1. BU SATIRI EKLEYİN
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

function AppRoutes() {
   const { user } = useAuth();
   
   return (
      <Routes>
        <Route 
          path="/login" 
          element={user ? <Navigate to="/" replace /> : <LoginPage onLoginSuccess={() => window.location.href = '/'} />} 
        />
        
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
        
        <Route
          path="/profile"
          element={
            <ProtectedRoute>
              <ProfilePage />
            </ProtectedRoute>
          }
        />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
   );
}

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
         <AppRoutes />
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;