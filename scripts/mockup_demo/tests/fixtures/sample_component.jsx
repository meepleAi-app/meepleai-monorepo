const App = () => (
  <div>
    <nav className="sidebar">
      <li className="nav-item">Dashboard</li>
      <li className="nav-item">Games</li>
    </nav>
    <button className="cta-primary" onClick={() => handlePlay()}>
      Avvia libro game
    </button>
    <div className="game-card">Card</div>
  </div>
);
