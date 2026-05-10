import { createContext, useContext, useState, useEffect } from 'react';
import api from '../api';

const AuthContext = createContext(null);

const COOKIE_NAME = "simplemaps_user";

const getCookie = (name) => {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? JSON.parse(decodeURIComponent(match[1])) : null;
};

const setCookie = (name, value) => {
  document.cookie = `${name}=${encodeURIComponent(JSON.stringify(value))}; path=/; SameSite=Lax`;
};

const deleteCookie = (name) => {
  document.cookie = `${name}=; path=/; max-age=0`;
};

export const AuthProvider = ({ children }) => {

  const [user, setUser] = useState(() => getCookie(COOKIE_NAME) ?? undefined);
  const [accessToken, setAccessToken] = useState(null);

  useEffect(() => {
    api.post('/api/refresh')
    .then(response => {
      const { accessToken, user } = response.data
      setAccessToken(accessToken);
      setUser(user);
      setCookie(COOKIE_NAME, user);
      api.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`
    })
    .catch(() => {
      setUser(null);
      deleteCookie(COOKIE_NAME);
    })
  }, [])

  const login = async (email, password) => {
    const response = await api.post('/api/login', { email, password });
    const { accessToken, user } = response.data
    api.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`;
    setAccessToken(accessToken);
    setUser(user)
    setCookie(COOKIE_NAME, user);
    return response;
  };

  const signup = async (username, email, password) => {
    const response = await api.post('/api/signup', { username, email, password });
    const { accessToken, user } = response.data; 
    api.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`;
    setAccessToken(accessToken);
    setUser(user);
    setCookie(COOKIE_NAME, user);
    return response;
  };

  const logout = async () => {
    await api.post('/api/logout');
    setAccessToken(null);
    setUser(null);
    deleteCookie(COOKIE_NAME);
  };

  return(
    <AuthContext.Provider value={{ user, login, signup, logout, accessToken }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => useContext(AuthContext);