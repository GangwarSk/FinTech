export interface CurrentUser {
  id: string;
  userName: string;
  email: string;
  fullName: string;
  mustChangePassword: boolean;
  lastLoginOnUtc?: string | null;
  roles: string[];
  permissions: string[];
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresOnUtc: string;
  refreshTokenExpiresOnUtc: string;
  user: CurrentUser;
}

export const Permissions = {
  TransactionsRead: 'transactions.read',
  TransactionsWrite: 'transactions.write',
  StatementsUpload: 'statements.upload',
  StatementsRead: 'statements.read',
  StatementsDelete: 'statements.delete',
  PersonsRead: 'persons.read',
  PersonsWrite: 'persons.write',
  MastersRead: 'masters.read',
  MastersWrite: 'masters.write',
  UsersManage: 'users.manage',
  SettingsManage: 'settings.manage',
  ReportsRead: 'reports.read',
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];
