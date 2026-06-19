import { Product, Restaurant, ThemeType } from './models';

const hours = [
  { day: 'Ponedjeljak', open: '09:00', close: '23:00', closed: false },
  { day: 'Utorak', open: '09:00', close: '23:00', closed: false },
  { day: 'Srijeda', open: '09:00', close: '23:00', closed: false },
  { day: 'Četvrtak', open: '09:00', close: '23:00', closed: false },
  { day: 'Petak', open: '09:00', close: '00:00', closed: false },
  { day: 'Subota', open: '10:00', close: '00:00', closed: false },
  { day: 'Nedjelja', open: '10:00', close: '22:00', closed: false },
];

export const themeOptions: { id: ThemeType; name: string; description: string; colors: string[] }[] = [
  { id: 'modern-dark', name: 'Modern Dark', description: 'Moderan tamni izgled za pizzerije, barove i klubove', colors: ['#09090b', '#27272a', '#e63946'] },
  { id: 'classic-light', name: 'Classic Light', description: 'Topao i elegantan izgled za restorane', colors: ['#fdf8f3', '#ffffff', '#c8a96e'] },
  { id: 'premium-gold', name: 'Premium Gold', description: 'Luksuzan izgled za premium objekte', colors: ['#0d0b08', '#1a1610', '#d4a72c'] },
  { id: 'natural-green', name: 'Natural Green', description: 'Svjež izgled za zdrave i prirodne menije', colors: ['#f3f7f3', '#ffffff', '#166534'] },
];

export const restaurants: Restaurant[] = [
  {
    id: 'old-town', name: 'Old Town Restaurant', address: 'Baščaršija 12, Sarajevo', phone: '+387 33 123 456',
    website: 'www.oldtown.ba', instagram: '@oldtown_restaurant', status: 'active', subscription: 'premium',
    theme: 'classic-light', themeColor: '#c8a96e', rating: 4.8, views: 2847,
    cover: 'https://images.unsplash.com/photo-1414235077428-338989a2e8c0?w=1200&h=600&fit=crop',
    logo: 'https://images.unsplash.com/photo-1590846406792-0adc7f938f1d?w=200&h=200&fit=crop',
    businessHours: hours,
    categories: [
      { id: 'traditional', name: 'Tradicionalna jela', icon: '🍲', order: 1, active: true },
      { id: 'grill', name: 'Roštilj', icon: '🔥', order: 2, active: true },
      { id: 'soups', name: 'Supe', icon: '🥣', order: 3, active: true },
      { id: 'salads', name: 'Salate', icon: '🥗', order: 4, active: true },
      { id: 'desserts', name: 'Deserti', icon: '🍰', order: 5, active: true },
      { id: 'drinks', name: 'Pića', icon: '☕', order: 6, active: true },
    ],
    dailyMenu: [{ id: 'dm1', name: 'Dnevni ručak', items: ['Goveđa čorba', 'Bosanski lonac', 'Sezonska salata', 'Baklava'], price: 22, date: 'Danas', active: true }],
    offers: [{ id: 'of1', name: 'Porodični vikend paket', description: 'Bosanski lonac za 4 osobe, piće i baklava.', originalPrice: 62, offerPrice: 49.9, image: 'https://images.unsplash.com/photo-1547592180-85f173990554?w=900&h=500&fit=crop', validUntil: '30.06.2026.', active: true }],
  },
  {
    id: 'pizzeria-roma', name: 'Pizzeria Roma', address: 'Ferhadija 28, Sarajevo', phone: '+387 33 456 789',
    website: 'www.pizzeriaroma.ba', instagram: '@pizzeria_roma_sa', status: 'active', subscription: 'premium',
    theme: 'modern-dark', themeColor: '#e63946', rating: 4.6, views: 3291,
    cover: 'https://images.unsplash.com/photo-1513104890138-7c749659a591?w=1200&h=600&fit=crop',
    logo: 'https://images.unsplash.com/photo-1565299624946-b28f40a0ae38?w=200&h=200&fit=crop', businessHours: hours,
    categories: [{ id: 'pizza', name: 'Pizza', icon: '🍕', order: 1, active: true }, { id: 'pasta', name: 'Pasta', icon: '🍝', order: 2, active: true }, { id: 'desserts', name: 'Deserti', icon: '🍰', order: 3, active: true }, { id: 'drinks', name: 'Pića', icon: '🥤', order: 4, active: true }],
    dailyMenu: [], offers: [{ id: 'of2', name: '2 pizze + tiramisu', description: 'Odaberi dvije pizze i dobijaš kućni tiramisu gratis.', originalPrice: 34, offerPrice: 28, image: 'https://images.unsplash.com/photo-1565299624946-b28f40a0ae38?w=900&h=500&fit=crop', validUntil: '25.06.2026.', active: true }],
  },
  {
    id: 'caffe-central', name: 'Caffe Central', address: 'Titova 15, Sarajevo', phone: '+387 33 789 012',
    website: 'www.central.ba', instagram: '@caffe_central', status: 'paused', subscription: 'basic',
    theme: 'natural-green', themeColor: '#166534', rating: 4.7, views: 1864,
    cover: 'https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?w=1200&h=600&fit=crop',
    logo: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=200&h=200&fit=crop', businessHours: hours,
    categories: [{ id: 'coffee', name: 'Kafa', icon: '☕', order: 1, active: true }, { id: 'cakes', name: 'Kolači', icon: '🍰', order: 2, active: true }, { id: 'sandwiches', name: 'Sendviči', icon: '🥪', order: 3, active: true }, { id: 'drinks', name: 'Pića', icon: '🥤', order: 4, active: true }],
    dailyMenu: [], offers: [],
  },
];

export const products: Record<string, Product[]> = {
  'old-town': [
    { id: 'ot1', categoryId: 'traditional', name: 'Bosanski lonac', description: 'Sporo kuhano tradicionalno jelo s mesom i sezonskim povrćem.', price: 18.5, image: 'https://images.unsplash.com/photo-1547592180-85f173990554?w=600&h=600&fit=crop', available: true, badges: ['popular', 'chefs-choice'], allergens: ['Gluten'] },
    { id: 'ot2', categoryId: 'traditional', name: 'Punjene paprike', description: 'Paprike punjene začinjenim mesom i rižom u paradajz sosu.', price: 14, image: 'https://images.unsplash.com/photo-1596097634834-cc6e1acce9bd?w=600&h=600&fit=crop', available: true, badges: ['popular'], allergens: ['Gluten'] },
    { id: 'ot3', categoryId: 'grill', name: 'Ćevapi sa kajmakom', description: 'Deset ćevapa, kajmak, luk i svježi somun.', price: 14, image: 'https://images.unsplash.com/photo-1529042410461-4d04b8e7d4db?w=600&h=600&fit=crop', available: true, badges: ['popular'], allergens: ['Gluten', 'Mlijeko'] },
    { id: 'ot4', categoryId: 'soups', name: 'Goveđa čorba', description: 'Bogata goveđa čorba s povrćem i domaćim rezancima.', price: 8, image: 'https://images.unsplash.com/photo-1476718406336-bb5a9690ee2a?w=600&h=600&fit=crop', available: true, badges: [], allergens: ['Gluten'] },
    { id: 'ot5', categoryId: 'salads', name: 'Šopska salata', description: 'Paradajz, krastavac, paprika, luk i feta sir.', price: 8, image: 'https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=600&h=600&fit=crop', available: true, badges: ['vegetarian'], allergens: ['Mlijeko'] },
    { id: 'ot6', categoryId: 'desserts', name: 'Baklava', description: 'Tradicionalna baklava s orasima i domaćom agdom.', price: 5.5, image: 'https://images.unsplash.com/photo-1519915028121-7d3463d20b13?w=600&h=600&fit=crop', available: true, badges: ['new'], allergens: ['Gluten', 'Orašasti plodovi'] },
  ],
  'pizzeria-roma': [
    { id: 'pr1', categoryId: 'pizza', name: 'Pizza Margherita', description: 'San Marzano paradajz, mozzarella, bosiljak i maslinovo ulje.', price: 12, image: 'https://images.unsplash.com/photo-1565299624946-b28f40a0ae38?w=600&h=600&fit=crop', available: true, badges: ['popular', 'vegetarian'], allergens: ['Gluten', 'Mlijeko'] },
    { id: 'pr2', categoryId: 'pizza', name: 'Pizza Diavola', description: 'Pikantna salama, paradajz, mozzarella i chili ulje.', price: 14.5, image: 'https://images.unsplash.com/photo-1574071318508-1cdbab80d002?w=600&h=600&fit=crop', available: true, badges: ['spicy'], allergens: ['Gluten', 'Mlijeko'] },
    { id: 'pr3', categoryId: 'pasta', name: 'Spaghetti Carbonara', description: 'Guanciale, jaja, Pecorino Romano i svježi biber.', price: 13, image: 'https://images.unsplash.com/photo-1612874742237-6526221588e3?w=600&h=600&fit=crop', available: true, badges: ['popular'], allergens: ['Gluten', 'Jaja', 'Mlijeko'] },
  ],
  'caffe-central': [
    { id: 'cc1', categoryId: 'coffee', name: 'Specialty Cappuccino', description: 'Dupli espresso i svilenkasta mliječna pjena.', price: 4.5, image: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=600&h=600&fit=crop', available: true, badges: ['popular'], allergens: ['Mlijeko'] },
    { id: 'cc2', categoryId: 'cakes', name: 'New York Cheesecake', description: 'Kremasti cheesecake s preljevom od šumskog voća.', price: 6.5, image: 'https://images.unsplash.com/photo-1565958011703-44f9829ba187?w=600&h=600&fit=crop', available: true, badges: ['chefs-choice'], allergens: ['Gluten', 'Jaja', 'Mlijeko'] },
  ],
};
