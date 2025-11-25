import React from 'react';
import { Plus, Loader, AlertCircle } from 'lucide-react';

export default function OrdersTab({ 
  orders, 
  products, 
  formData, 
  setFormData, 
  loading, 
  createOrder, 
  handleProductChange 
}) {
  
  const getStatusColor = (status) => {
    const colors = {
      'Pending': 'bg-yellow-500/20 text-yellow-300 border-yellow-500/30',
      'StockReserved': 'bg-blue-500/20 text-blue-300 border-blue-500/30',
      'Approved': 'bg-green-500/20 text-green-300 border-green-500/30',
      'PaymentCompleted': 'bg-purple-500/20 text-purple-300 border-purple-500/30',
      'Shipped': 'bg-cyan-500/20 text-cyan-300 border-cyan-500/30',
      'Delivered': 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30',
      'Cancelled': 'bg-gray-500/20 text-gray-400 border-gray-500/30',
      'Failed': 'bg-red-500/20 text-red-300 border-red-500/30'
    };
    return colors[status] || 'bg-gray-500/20 text-gray-300 border-gray-500/30';
  };

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
      {/* Create Order Form */}
      <div className="lg:col-span-1">
        <div className="bg-slate-800 rounded-xl border border-slate-700 p-6 shadow-xl sticky top-28">
          <h2 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
            <Plus className="w-5 h-5" /> Yeni Sipariş
          </h2>
          <form onSubmit={createOrder} className="space-y-4">
            <input
              type="text"
              placeholder="Müşteri Adı"
              value={formData.customerName}
              onChange={e => setFormData({...formData, customerName: e.target.value})}
              className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400"
              required
            />
            <input
              type="email"
              placeholder="E-posta"
              value={formData.customerEmail}
              onChange={e => setFormData({...formData, customerEmail: e.target.value})}
              className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400"
              required
            />
            <input
              type="tel"
              placeholder="Telefon"
              value={formData.customerPhone}
              onChange={e => setFormData({...formData, customerPhone: e.target.value})}
              className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400"
            />
            <div className="border-t border-slate-600 pt-4 space-y-3">
              <label className="text-sm text-slate-300 font-medium block">Sipariş Edilecek Ürün</label>
              
              <select
                  value={formData.items[0].productId}
                  onChange={handleProductChange}
                  className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white"
                  required
              >
                  <option value="" disabled>--- Ürün Seçiniz ---</option>
                  {products.map(product => (
                      <option key={product.id} value={product.id} disabled={product.availableQuantity <= 0}>
                          {product.name} ({product.availableQuantity} stok kaldı)
                      </option>
                  ))}
              </select>

              <input
                type="number"
                placeholder="Adet"
                min="1"
                max={products.find(p => p.id === formData.items[0].productId)?.availableQuantity || 999}
                value={formData.items[0].quantity}
                onChange={e => {
                  const items = [...formData.items];
                  items[0].quantity = parseInt(e.target.value) || 1;
                  setFormData({...formData, items});
                }}
                className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400"
                disabled={!formData.items[0].productId}
                required
              />
              <input
                type="number"
                placeholder="Fiyat (Birim)"
                min="0"
                step="0.01"
                value={formData.items[0].unitPrice}
                onChange={e => {
                  const items = [...formData.items];
                  items[0].unitPrice = parseFloat(e.target.value) || 0;
                  setFormData({...formData, items});
                }}
                className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400"
                disabled={!formData.items[0].productId}
                required
              />
            </div>
            <button
              type="submit"
              disabled={loading || !formData.items[0].productId || formData.items[0].quantity <= 0}
              className="w-full bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 disabled:opacity-50 text-white font-bold py-2 rounded transition-all flex items-center justify-center gap-2"
            >
              {loading ? <Loader className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
              Sipariş Oluştur ({ (formData.items[0].quantity * formData.items[0].unitPrice).toFixed(2) } ₺)
            </button>
          </form>
        </div>
      </div>

      {/* Orders List */}
      <div className="lg:col-span-2">
        <div className="bg-slate-800 rounded-xl border border-slate-700 p-6 shadow-xl h-full">
          <h2 className="text-lg font-bold text-white mb-4">Son Siparişler (Yeni &gt; Eski)</h2>
          {orders.length === 0 ? (
            <div className="text-slate-400 text-center py-8">
              <AlertCircle className="w-8 h-8 mx-auto mb-2 opacity-50" />
              Henüz sipariş yok
            </div>
          ) : (
            <div className="space-y-3 max-h-[80vh] lg:max-h-[70vh] overflow-y-auto pr-2">
              {orders.map(order => (
                <div key={order.id} className="bg-slate-700 rounded-lg p-4 border border-slate-600 hover:bg-slate-600/50 transition-all">
                  <div className="flex justify-between items-start mb-2">
                    <div>
                      <p className="text-white font-semibold">{order.customerName}</p>
                      <p className="text-slate-400 text-sm">{order.customerEmail}</p>
                    </div>
                    <span className={`px-2 py-1 rounded text-xs font-semibold border ${getStatusColor(order.status)}`}>
                        {order.status}
                    </span>
                  </div>
                  <p className="text-blue-400 font-bold">{order.totalAmount.toFixed(2)} ₺</p>
                  <div className="text-slate-400 text-xs mt-1 flex justify-between">
                      <span>ID: {order.id.substring(0, 8)}...</span>
                      <span>{new Date(order.createdAt).toLocaleString('tr-TR')}</span>
                  </div>
                  <div className="mt-2 pt-2 border-t border-slate-600">
                       {order.items.map((item, index) => (
                          <p key={index} className="text-xs text-slate-300">
                              {item.productName} ({item.quantity} adet) - {item.unitPrice.toFixed(2)}₺
                          </p>
                       ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}