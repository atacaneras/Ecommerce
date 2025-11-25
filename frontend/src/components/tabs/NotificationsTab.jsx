import React from 'react';
import { Bell, CheckCircle, AlertCircle } from 'lucide-react';

export default function NotificationsTab({ notifications }) {
  return (
    <div className="bg-slate-800 rounded-xl border border-slate-700 p-6 shadow-xl">
      <h2 className="text-lg font-bold text-white mb-4">Bildirimler</h2>
      {notifications.length === 0 ? (
        <div className="text-slate-400 text-center py-12">
          <Bell className="w-8 h-8 mx-auto mb-2 opacity-50" />
          Henüz bildirim yok
        </div>
      ) : (
        <div className="space-y-3 max-h-[80vh] overflow-y-auto pr-2">
          {notifications.map(notif => (
            <div key={notif.id} className="bg-slate-700 rounded-lg p-4 border-l-4 border-blue-500 hover:bg-slate-600/50 transition-all">
              <div className="flex items-start justify-between">
                <div className="flex-1">
                  <p className="text-white font-semibold">{notif.type} - {notif.recipient}</p>
                  <p className="text-slate-300 text-sm mt-1">{notif.message}</p>
                  <p className="text-slate-400 text-xs mt-2">{new Date(notif.createdAt).toLocaleString('tr-TR')}</p>
                </div>
                {notif.status === 'Sent' ? (
                  <CheckCircle className="w-5 h-5 text-green-400 flex-shrink-0 ml-4" />
                ) : (
                  <AlertCircle className="w-5 h-5 text-yellow-400 flex-shrink-0 ml-4" />
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}