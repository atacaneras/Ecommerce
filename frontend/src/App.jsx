import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import LoginPage from './components/auth/LoginPage';
import './App.css';

const ProtectedRoute = ({ children }) => {
  const token = localStorage.getItem('token'); // Token'ı localStorage'dan kontrol et
  if (!token) {
    return <Navigate to="/login" replace />;
  }
  return children;
};

function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Login Rotası */}
        <Route path="/login" element={<LoginPage />} />
        
        {/* Ana Sayfa (Dashboard) - Korumalı */}
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

        {/* Bilinmeyen rotaları dashboard'a yönlendir */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;