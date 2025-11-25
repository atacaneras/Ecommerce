import React, { useState, useEffect } from 'react';
import { ShoppingCart, Package, Bell, ClipboardCheck } from 'lucide-react';

// Import sub-components
import OrdersTab from './tabs/OrdersTab';
import ProductsTab from './tabs/ProductsTab';
import VerificationsTab from './tabs/VerificationsTab';
import NotificationsTab from './tabs/NotificationsTab';

export default function Dashboard() {
  const [orders, setOrders] = useState([]);
  const [products, setProducts] = useState([]);
  const [notifications, setNotifications] = useState([]);
  const [verifications, setVerifications] = useState([]);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('orders');
  
  const [formData, setFormData] = useState({
    customerName: '',
    customerEmail: '',
    customerPhone: '',
    items: [{ productId: '', productName: '', quantity: 1, unitPrice: 0 }]
  });
  
  const [newProduct, setNewProduct] = useState({
    name: '',
    description: '',
    price: 0,
    stockQuantity: 0
  });

  const API_BASE = 'http://localhost:5001';
  const STOCK_API = 'http://localhost:5002';
  const NOTIFICATION_API = 'http://localhost:5003';
  const VERIFICATION_API = 'http://localhost:5004';

  useEffect(() => {
    fetchOrders();
    fetchProducts();
    fetchNotifications();
    fetchVerifications();
    const interval = setInterval(() => {
      fetchOrders();
      fetchProducts();
      fetchNotifications();
      fetchVerifications();
    }, 5000);
    return () => clearInterval(interval);
  }, []);

  const fetchOrders = async () => {
    try {
      const res = await fetch(`${API_BASE}/api/orders`);
      if (res.ok) {
        const data = await res.json();
        const sortedData = (data || []).sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
        setOrders(sortedData);
      }
    } catch (err) {
      console.error('Siparişler yüklenemedi:', err);
    }
  };

  const fetchProducts = async () => {
    try {
      const res = await fetch(`${STOCK_API}/api/stock/products`);
      if (res.ok) {
        const data = await res.json();
        setProducts(data || []);
      }
    } catch (err) {
      console.error('Ürünler yüklenemedi:', err);
    }
  };

  const fetchNotifications = async () => {
    try {
      const res = await fetch(`${NOTIFICATION_API}/api/notifications`);
      if (res.ok) {
        const data = await res.json();
        setNotifications(data || []);
      }
    } catch (err) {
      console.error('Bildirimler yüklenemedi:', err);
    }
  };

  const fetchVerifications = async () => {
    try {
      const res = await fetch(`${VERIFICATION_API}/api/verification`);
      if (res.ok) {
        const data = await res.json();
        setVerifications(data || []);
      }
    } catch (err) {
      console.error('Doğrulamalar yüklenemedi:', err);
    }
  };

  const handleProductChange = (e) => {
    const selectedProductId = parseInt(e.target.value);
    const selectedProduct = products.find(p => p.id === selectedProductId);

    if (selectedProduct) {
        const items = [...formData.items];
        items[0] = {
            productId: selectedProduct.id,
            productName: selectedProduct.name,
            quantity: 1,
            unitPrice: selectedProduct.price
        };
        setFormData({...formData, items});
    }
  };

  const createOrder = async (e) => {
    e.preventDefault();
    const currentProduct = products.find(p => p.id === formData.items[0].productId);

    if (!formData.customerName || !formData.customerEmail || !formData.items[0].productId) {
      alert('Lütfen müşteri ve ürün bilgilerini doldurunuz');
      return;
    }

    if (currentProduct && formData.items[0].quantity > currentProduct.availableQuantity) {
        alert(`Yetersiz stok! Bu ürün için maksimum ${currentProduct.availableQuantity} adet sipariş verebilirsiniz.`);
        return;
    }

    setLoading(true);

    const orderDataToSend = {
        customerName: formData.customerName,
        customerEmail: formData.customerEmail,
        customerPhone: formData.customerPhone,
        items: formData.items.map(item => ({
            productId: item.productId,
            productName: item.productName,
            quantity: item.quantity,
            unitPrice: item.unitPrice
        }))
    };

    try {
      const res = await fetch(`${API_BASE}/api/orders`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(orderDataToSend)
      });
      
      if (res.ok) {
        const result = await res.json();
        alert(`Sipariş başarıyla oluşturuldu! Sipariş ID: ${result.id.substring(0, 8)}... Durum: ${result.status}`);
        
        setFormData(prev => ({
          customerName: '',
          customerEmail: '',
          customerPhone: '',
          items: [{ 
              productId: prev.items[0].productId, 
              productName: prev.items[0].productName, 
              quantity: 1, 
              unitPrice: prev.items[0].unitPrice 
          }]
        }));
        fetchOrders();
        fetchProducts(); 
        fetchVerifications();
      } else {
        const errorData = await res.json();
        alert('Sipariş oluşturulamadı. Hata: ' + (errorData.message || res.statusText));
      }
    } catch (err) {
      alert('Hata: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const createProduct = async (e) => {
    e.preventDefault();
    if (!newProduct.name || !newProduct.price || newProduct.stockQuantity <= 0) {
      alert('Lütfen geçerli ürün bilgilerini doldurunuz');
      return;
    }
    setLoading(true);
    try {
      const res = await fetch(`${STOCK_API}/api/stock`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newProduct)
      });
      if (res.ok) {
        alert('Ürün oluşturuldu!');
        setNewProduct({ name: '', description: '', price: 0, stockQuantity: 0 });
        fetchProducts();
      } else {
        alert('Ürün oluşturulamadı');
      }
    } catch (err) {
      alert('Hata: ' + err.message);
    } finally {
      setLoading(false);
    }
  };

  const deleteProduct = async (productId) => {
    if (!window.confirm(`Ürünü (ID: ${productId}) silmek istediğinizden emin misiniz?`)) {
        return;
    }

    setLoading(true);
    try {
        const res = await fetch(`${STOCK_API}/api/stock/${productId}`, {
            method: 'DELETE',
        });
        
        if (res.status === 204) {
            alert('Ürün başarıyla silindi!');
            fetchProducts();
        } else if (res.status === 404) {
            alert('Ürün bulunamadı.');
        } else {
            alert('Ürün silinirken bir hata oluştu.');
        }
    } catch (err) {
        alert('Hata: ' + err.message);
    } finally {
        setLoading(false);
    }
  };
  
  const approveOrder = async (orderId) => {
    if (!window.confirm(`Sipariş ID ${orderId.substring(0, 8)}...'i onaylamak istediğinizden emin misiniz?`)) {
        return;
    }
    setLoading(true);
    try {
        const res = await fetch(`${VERIFICATION_API}/api/verification/approve/${orderId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
        });

        if (res.ok) {
            alert('Sipariş başarıyla onaylandı!');
            fetchOrders();
            fetchVerifications();
            fetchProducts();
        } else {
            const errorData = await res.json();
            alert('Sipariş onaylanırken hata oluştu: ' + (errorData.message || res.statusText));
        }
    } catch (err) {
        alert('Onaylama sırasında bağlantı hatası: ' + err.message);
    } finally {
        setLoading(false);
    }
  };

  const cancelOrder = async (orderId) => {
    if (!window.confirm(`Sipariş ID ${orderId.substring(0, 8)}...'i iptal etmek istediğinizden emin misiniz?`)) {
        return;
    }
    setLoading(true);
    try {
        const res = await fetch(`${API_BASE}/api/orders/cancel/${orderId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
        });

        if (res.ok) {
            alert('Sipariş başarıyla iptal edildi!');
            // Verifications'ı önce güncelle, sonra diğerlerini
            await fetchVerifications();
            await fetchOrders();
            await fetchProducts();
        } else {
            const errorData = await res.json();
            alert('Sipariş iptal edilirken hata oluştu: ' + (errorData.message || res.statusText));
        }
    } catch (err) {
        alert('İptal sırasında bağlantı hatası: ' + err.message);
    } finally {
        setLoading(false);
    }
  };

  const pendingVerifications = verifications.filter(v => v.status === 'Pending');

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900">
      {/* Header */}
      <header className="bg-slate-950 border-b border-slate-700 sticky top-0 z-50">
        <div className="w-full px-4 py-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="bg-gradient-to-br from-blue-500 to-purple-600 p-2 rounded-lg">
                <ShoppingCart className="w-6 h-6 text-white" />
              </div>
              <h1 className="text-2xl font-bold text-white">E-Ticaret Mikroservis Dashboard</h1>
            </div>
            <div className="flex gap-2">
              <div className="px-3 py-1 bg-green-500/20 text-green-300 rounded-full text-sm border border-green-500/30">
                ✓ Servislere Bağlı
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Tabs */}
      <div className="bg-slate-900 border-b border-slate-700 sticky top-16 z-40">
        <div className="w-full px-4 sm:px-6 lg:px-8">
          <div className="flex gap-1">
            {[
              { id: 'orders', label: `Siparişler (${orders.length})`, icon: ShoppingCart },
              { id: 'verifications', label: `Sipariş Onay (${pendingVerifications.length})`, icon: ClipboardCheck },
              { id: 'products', label: `Ürünler (${products.length})`, icon: Package },
              { id: 'notifications', label: `Bildirimler (${notifications.length})`, icon: Bell }
            ].map(tab => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`px-4 py-3 font-medium transition-all flex items-center gap-2 border-b-2 ${
                  activeTab === tab.id
                    ? 'border-blue-500 text-blue-400'
                    : 'border-transparent text-slate-400 hover:text-slate-300'
                }`}
              >
                <tab.icon className="w-4 h-4" />
                {tab.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Content */}
      <main className="w-full px-4 py-8 sm:px-6 lg:px-8">
        
        {activeTab === 'orders' && (
          <OrdersTab 
            orders={orders} 
            products={products}
            formData={formData}
            setFormData={setFormData}
            loading={loading}
            createOrder={createOrder}
            handleProductChange={handleProductChange}
          />
        )}

        {activeTab === 'verifications' && (
          <VerificationsTab 
            verifications={verifications}
            approveOrder={approveOrder}
            cancelOrder={cancelOrder}
            loading={loading}
          />
        )}

        {activeTab === 'products' && (
          <ProductsTab 
            products={products}
            newProduct={newProduct}
            setNewProduct={setNewProduct}
            loading={loading}
            createProduct={createProduct}
            deleteProduct={deleteProduct}
          />
        )}

        {activeTab === 'notifications' && (
          <NotificationsTab notifications={notifications} />
        )}

      </main>
    </div>
  );
}