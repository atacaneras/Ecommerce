import React from 'react';
import { FileText, CheckCircle, XCircle, Clock } from 'lucide-react';

export default function InvoicesTab({ invoices }) {
  const getStatusColor = (status) => {
    const colors = {
      'Draft': 'bg-gray-500/20 text-gray-300 border-gray-500/30',
      'Issued': 'bg-blue-500/20 text-blue-300 border-blue-500/30',
      'Paid': 'bg-green-500/20 text-green-300 border-green-500/30',
      'Cancelled': 'bg-red-500/20 text-red-300 border-red-500/30',
      'Overdue': 'bg-orange-500/20 text-orange-300 border-orange-500/30'
    };
    return colors[status] || 'bg-gray-500/20 text-gray-300 border-gray-500/30';
  };

  const getStatusIcon = (status) => {
    switch(status) {
      case 'Paid':
        return <CheckCircle className="w-5 h-5 text-green-400" />;
      case 'Cancelled':
        return <XCircle className="w-5 h-5 text-red-400" />;
      case 'Issued':
        return <Clock className="w-5 h-5 text-blue-400" />;
      default:
        return <FileText className="w-5 h-5 text-gray-400" />;
    }
  };

  return (
    <div className="bg-slate-800 rounded-xl border border-slate-700 p-6 shadow-xl">
      <h2 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
        <FileText className="w-5 h-5" />
        Faturalar
      </h2>
      {invoices.length === 0 ? (
        <div className="text-slate-400 text-center py-12">
          <FileText className="w-8 h-8 mx-auto mb-2 opacity-50" />
          Henüz fatura yok
        </div>
      ) : (
        <div className="space-y-3 max-h-[80vh] overflow-y-auto pr-2">
          {invoices.map(invoice => (
            <div key={invoice.id} className="bg-slate-700 rounded-lg p-4 border border-slate-600 hover:bg-slate-600/50 transition-all">
              <div className="flex justify-between items-start mb-3">
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    {getStatusIcon(invoice.status)}
                    <span className="text-white font-bold text-lg">{invoice.invoiceNumber}</span>
                  </div>
                  <p className="text-slate-300">{invoice.customerName}</p>
                  <p className="text-slate-400 text-sm">Sipariş ID: {invoice.orderId.substring(0, 8)}...</p>
                </div>
                <span className={`px-2 py-1 rounded text-xs font-semibold border ${getStatusColor(invoice.status)}`}>
                  {invoice.status}
                </span>
              </div>

              {/* Invoice Details */}
              <div className="bg-slate-800 rounded-lg p-3 mt-3 space-y-2">
                <div className="flex justify-between text-sm">
                  <span className="text-slate-400">Ara Toplam:</span>
                  <span className="text-white font-semibold">{invoice.subTotal.toFixed(2)} ₺</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-slate-400">KDV (%{invoice.taxRate}):</span>
                  <span className="text-white font-semibold">{invoice.taxAmount.toFixed(2)} ₺</span>
                </div>
                <div className="flex justify-between text-base border-t border-slate-600 pt-2 mt-2">
                  <span className="text-white font-bold">TOPLAM:</span>
                  <span className="text-green-400 font-bold text-xl">{invoice.totalAmount.toFixed(2)} ₺</span>
                </div>
              </div>

              {/* Invoice Items */}
              {invoice.items && invoice.items.length > 0 && (
                <div className="mt-3 pt-3 border-t border-slate-600">
                  <p className="text-xs text-slate-400 mb-2">Ürünler:</p>
                  {invoice.items.map((item, index) => (
                    <div key={index} className="flex justify-between text-xs text-slate-300 py-1">
                      <span>{item.productName}</span>
                      <span>{item.quantity} x {item.unitPrice.toFixed(2)} ₺</span>
                    </div>
                  ))}
                </div>
              )}

              {/* Dates */}
              <div className="text-slate-400 text-xs mt-3 pt-3 border-t border-slate-600 flex justify-between">
                <span>Oluşturulma: {new Date(invoice.createdAt).toLocaleDateString('tr-TR')}</span>
                <span>Vade: {new Date(invoice.dueDate).toLocaleDateString('tr-TR')}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}