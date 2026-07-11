import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';

import PublicLayout from './components/PublicLayout/PublicLayout.jsx';
import Home from './components/Home/Home.jsx';
import Dashboard from './protected/Dashboard/Dashboard.jsx';

const App = () => {
  return (
    <BrowserRouter>
      <Routes>
        <Route path='/' element={<PublicLayout><Home /></PublicLayout>} />
        <Route path='/dashboard' element={<Dashboard />} />
        <Route path='*' element={<Navigate to='/' />} />
      </Routes>
    </BrowserRouter>
  );
};

export default App;
