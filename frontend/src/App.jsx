import React, { useState } from 'react';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import LoginPage from './components/auth/LoginPage';
import ProtectedRoute from './components/auth/ProtectedRoute';
import Dashboard from './components/Dashboard';
import './App.css';

function AppContent() {
  const { isAuthenticated } = useAuth();
  const [showDashboard, setShowDashboard] = useState(false);

  if (!isAuthenticated && !showDashboard) {
    return <LoginPage onLoginSuccess={() => setShowDashboard(true)} />;
  }

  return (
    <ProtectedRoute>
      <div className="app-container">
        <Dashboard />
      </div>
    </ProtectedRoute>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppContent />
    </AuthProvider>
  );
}

export default App;