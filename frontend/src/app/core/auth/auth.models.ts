export type UserRole = 'SuperAdmin' | 'RestaurantOwner' | 'RestaurantStaff';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface AuthSession {
  accessToken: string;
  expiresAt: string;
  role: UserRole;
  restaurantId: string | null;
}
