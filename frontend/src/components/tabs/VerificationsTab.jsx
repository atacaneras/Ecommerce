import React from 'react';
import { ClipboardCheck, Loader, CheckCircle, XCircle } from 'lucide-react';

export default function VerificationsTab({ verifications, approveOrder, cancelOrder, loading }) {
  const pendingVerifications = verifications.filter(v => v.status === 'Pending');

  return (
    <div className="bg-slate-800 rounded-xl border border-slate-700 p-6 shadow-xl">
      <h2 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
        <ClipboardCheck className="w-5 h-5" />
        Onay Bekleyen Siparişler
      </h2>
      {pendingVerifications.length === 0 ? (
        <div className="text-slate-400 text-center py-12">
          <ClipboardCheck className="w-8 h-8 mx-auto mb-2 opacity-50" />
          Onay bekleyen sipariş yok
        </div>
      ) : (
        <div className="space-y-3 max-h-[80vh] overflow-y-auto pr-2">
          {pendingVerifications.map(verification => (
            <div key={verification.id} className="bg-slate-700 rounded-lg p-4 border border-yellow-500/30 hover:bg-slate-600/50 transition-all">
              <div className="flex justify-between items-start mb-2">
                <div className="flex-1">
                  <p className="text-white font-semibold text-lg">{verification.customerName}</p>
                  <p className="text-slate-300 text-sm mt-1">Sipariş ID: {verification.orderId.substring(0, 8)}...</p>
                  <p className="text-blue-400 font-bold text-xl mt-2">{verification.totalAmount.toFixed(2)} ₺</p>
                  <p className="text-slate-400 text-xs mt-2">
                    {new Date(verification.createdAt).toLocaleString('tr-TR')}
                  </p>
                </div>
                <div className="flex gap-2">
                  <button 
                    onClick={() => approveOrder(verification.orderId)}
                    disabled={loading}
                    className="px-4 py-2 bg-green-600 text-white font-medium rounded-lg hover:bg-green-700 transition-all disabled:opacity-50 flex items-center gap-2"
                  >
                    {loading ? <Loader className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
                    Onayla
                  </button>
                  <button 
                    onClick={() => cancelOrder(verification.orderId)}
                    disabled={loading}
                    className="px-4 py-2 bg-red-600 text-white font-medium rounded-lg hover:bg-red-700 transition-all disabled:opacity-50 flex items-center gap-2"
                  >
                    {loading ? <Loader className="w-4 h-4 animate-spin" /> : <XCircle className="w-4 h-4" />}
                    İptal Et
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}