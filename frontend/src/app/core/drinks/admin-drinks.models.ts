export interface AdminGlobalDrink {
  id: string;
  name: string;
  slug: string;
  category: string;
  description: string | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
  updatedAt: string;
}

export interface GlobalDrinkRequest {
  name: string;
  slug: string | null;
  category: string;
  description: string | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
}
