import { Link } from 'react-router-dom';
import './PublicLayout.css';

const PublicLayout = ({ children }) => {
  return (
    <div className="public-layout">
      <header className="public-nav">
        <Link to="/" className="public-nav-logo">
          <span className="public-nav-logo-icon">GR</span>
          <span className="public-nav-logo-text">GeoRoute</span>
        </Link>
        <nav className="public-nav-links">
          <Link to="/dashboard" className="public-nav-signup">Open Map</Link>
        </nav>
      </header>
      <main className="public-main">
        {children}
      </main>
    </div>
  );
};

export default PublicLayout;
