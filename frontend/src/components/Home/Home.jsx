import { Link } from 'react-router-dom';
import './Home.css';

const Home = () => {
  return (
    <div className="home-page">
      <section className="home-hero">
        <h1 className="home-hero-title">SimpleMaps</h1>
        <p className="home-hero-sub">
          Simple Maps lets you query vehicle routes powered by a self-hosted routing engine
          built on OpenStreetMap data.
        </p>
        <div className="home-hero-ctas">
          <Link to="/signup" className="home-cta home-cta--primary">Get Started</Link>
          <Link to="/login" className="home-cta home-cta--secondary">Log In</Link>
        </div>
      </section>

      <section className="home-features">
        <div className="home-feature-card">
          <h2>Vehicle Routing</h2>
          <p>Fast car routes using A* pathfinding on real OpenStreetMap road data with one-way street support.</p>
        </div>
        <div className="home-feature-card">
          <h2>Self-Hosted</h2>
          <p>No third-party APIs. Routes are computed entirely on your own infrastructure using pgRouting and PostGIS.</p>
        </div>
        <div className="home-feature-card">
          <h2>Redis Caching</h2>
          <p>Repeated routes are served in milliseconds from cache, keeping the routing engine fast under load.</p>
        </div>
      </section>
    </div>
  );
};

export default Home;
