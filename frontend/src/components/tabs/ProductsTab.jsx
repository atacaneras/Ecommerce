import React from 'react';
import { Plus, Loader, Package, Trash2 } from 'lucide-react';

export default function ProductsTab({ 
  products, 
  newProduct, 
  setNewProduct, 
  loading, 
  createProduct, 
  deleteProduct 
}) {
  
  const getProductDisplayInfo = (product) => {
      const initialStock = product.stockQuantity; 
      const reserved = product.reservedQuantity;
      const available = product.availableQuantity;
      
      return (
          <>
              <p className="text-sm text-slate-400 mt-1">
                  Kalan: <span className="font-bold text-white">{available}</span>
                  <span className="text-slate-500 mx-2">|</span>
                  Rezerve: <span className="font-bold text-yellow-300">{reserved}</span>
              </p>
              <p className="text-xs text-slate-500 mt-1">
                  (İlk Stok: {initialStock})
              </p>
          </>
      );
  };

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
      {/* Create Product Form */}
      <div className="lg:col-span-1">
        <div className="bg-slate-800 rounded-xl border border-slate-700 p-6 shadow-xl sticky top-28">
          <h2 className="text-lg font-bold text-white mb-4 flex items-center gap-2">
            <Plus className="w-5 h-5" /> Yeni Ürün
          </h2>
          <form onSubmit={createProduct} className="space-y-4">
            <input
              type="text"
              placeholder="Ürün Adı"
              value={newProduct.name}
              onChange={e => setNewProduct({...newProduct, name: e.target.value})}
              className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400"
              required
            />
            <textarea
              placeholder="Açıklama"
              value={newProduct.description}
              onChange={e => setNewProduct({...newProduct, description: e.target.value})}
              className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400 h-20 resize-none"
            />
            <input
              type="number"
              placeholder="Fiyat"
              min="0"
              step="0.01"
              value={newProduct.price}
              onChange={e => setNewProduct({...newProduct, price: parseFloat(e.target.value) || 0})}
              className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400"
              required
            />
            <input
              type="number"
              placeholder="Stok Miktarı"
              min="0"
              value={newProduct.stockQuantity}
              onChange={e => setNewProduct({...newProduct, stockQuantity: parseInt(e.target.value) || 0})}
              className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-white placeholder-slate-400"
              required
            />
            <button
              type="submit"
              disabled={loading}
              className="w-full bg-gradient-to-r from-green-600 to-emerald-600 hover:from-green-700 hover:to-emerald-700 disabled:opacity-50 text-white font-bold py-2 rounded transition-all flex items-center justify-center gap-2"
            >
              {loading ? <Loader className="w-4 h-4 animate-spin" /> : <Plus className="w-4 h-4" />}
              Ürün Ekle
            </button>
          </form>
        </div>
      </div>

      {/* Products Grid */}
      <div className="lg:col-span-2">
        {products.length === 0 ? (
          <div className="bg-slate-800 rounded-xl border border-slate-700 p-6 text-slate-400 text-center">
            <Package className="w-8 h-8 mx-auto mb-2 opacity-50" />
            Henüz ürün yok
          </div>
        ) : (
          <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {products.map(product => (
              <div key={product.id} className="bg-slate-800 rounded-xl border border-slate-700 p-4 shadow-xl hover:border-slate-600 transition-all hover-lift">
                <div className="flex justify-between items-start mb-2">
                  <h3 className="text-white font-bold text-lg">{product.name}</h3>
                  <button 
                      onClick={() => deleteProduct(product.id)}
                      disabled={loading}
                      className="text-red-400 hover:text-red-500 disabled:opacity-50 p-1 rounded hover:bg-slate-700 transition-colors"
                      title="Ürünü Sil"
                  >
                      <Trash2 className="w-5 h-5" />
                  </button>
                </div>
                
                <p className="text-slate-400 text-sm mb-3 line-clamp-2 min-h-[3rem]">{product.description || "Açıklama yok"}</p>
                
                <div className="flex justify-between items-end border-t border-slate-700 pt-3">
                  <div>
                    <p className="text-blue-400 font-bold text-xl">{product.price.toFixed(2)} ₺</p>
                    {getProductDisplayInfo(product)}
                  </div>
                  <div className="text-right">
                    {product.availableQuantity > 0 ? (
                      <span className="bg-green-500/20 text-green-300 px-2 py-1 rounded text-xs font-semibold border border-green-500/30">
                        Müsait
                      </span>
                    ) : (
                      <span className="bg-red-500/20 text-red-300 px-2 py-1 rounded text-xs font-semibold border border-red-500/30">
                        Tükendi
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}