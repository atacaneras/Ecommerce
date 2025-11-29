import React, { useState, useRef, useEffect } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { User, LogOut, Settings, Shield, ChevronDown } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

export default function UserMenu() {
    const { user, logout, isAdmin } = useAuth();
    const [isOpen, setIsOpen] = useState(false);
    const menuRef = useRef(null);
    const navigate = useNavigate();

    useEffect(() => {
        const handleClickOutside = (event) => {
            if (menuRef.current && !menuRef.current.contains(event.target)) {
                setIsOpen(false);
            }
        };

        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    const handleLogout = async () => {
        await logout();
        setIsOpen(false);
        navigate('/login');
    };

    if (!user) return null;

    const getInitials = () => {
        if (user.firstName && user.lastName) {
            return `${user.firstName[0]}${user.lastName[0]}`.toUpperCase();
        }
        return user.username.substring(0, 2).toUpperCase();
    };

    const getDisplayName = () => {
        if (user.firstName && user.lastName) {
            return `${user.firstName} ${user.lastName}`;
        }
        return user.username;
    };

    return (
        <div className="relative" ref={menuRef}>
            {/* User Button */}
            <button
                onClick={() => setIsOpen(!isOpen)}
                className="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-slate-800 transition-colors"
            >
                <div className="flex items-center gap-3">
                    {/* Avatar */}
                    <div className="w-10 h-10 rounded-full bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center text-white font-bold text-sm">
                        {getInitials()}
                    </div>

                    {/* User Info */}
                    <div className="text-left hidden md:block">
                        <p className="text-white font-medium text-sm">{getDisplayName()}</p>
                        <p className="text-slate-400 text-xs flex items-center gap-1">
                            {isAdmin && <Shield className="w-3 h-3" />}
                            {user.role}
                        </p>
                    </div>
                </div>

                <ChevronDown className={`w-4 h-4 text-slate-400 transition-transform ${isOpen ? 'rotate-180' : ''}`} />
            </button>

            {/* Dropdown Menu */}
            {isOpen && (
                <div className="absolute right-0 mt-2 w-64 bg-slate-800 rounded-lg shadow-xl border border-slate-700 py-2 z-50">
                    {/* User Info Header */}
                    <div className="px-4 py-3 border-b border-slate-700">
                        <p className="text-white font-medium">{getDisplayName()}</p>
                        <p className="text-slate-400 text-sm">{user.email}</p>
                        <div className="mt-2 flex items-center gap-2">
                            <span className={`px-2 py-0.5 rounded text-xs font-medium ${isAdmin
                                ? 'bg-purple-500/20 text-purple-300 border border-purple-500/30'
                                : 'bg-blue-500/20 text-blue-300 border border-blue-500/30'
                                }`}>
                                {user.role}
                            </span>
                        </div>
                    </div>

                    {/* Menu Items */}
                    <div className="py-2">
                        <button
                            onClick={() => {
                                setIsOpen(false);
                                navigate('/profile');
                            }}
                            className="w-full px-4 py-2 text-left text-slate-300 hover:bg-slate-700 hover:text-white transition-colors flex items-center gap-3"
                        >
                            <User className="w-4 h-4" />
                            Profil
                        </button>

                        <button
                            onClick={() => {
                                setIsOpen(false);
                                // Navigate to settings (implement if needed)
                            }}
                            className="w-full px-4 py-2 text-left text-slate-300 hover:bg-slate-700 hover:text-white transition-colors flex items-center gap-3"
                        >
                            <Settings className="w-4 h-4" />
                            Ayarlar
                        </button>
                    </div>

                    {/* Logout */}
                    <div className="border-t border-slate-700 pt-2">
                        <button
                            onClick={handleLogout}
                            className="w-full px-4 py-2 text-left text-red-400 hover:bg-slate-700 hover:text-red-300 transition-colors flex items-center gap-3"
                        >
                            <LogOut className="w-4 h-4" />
                            Çıkış Yap
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
}