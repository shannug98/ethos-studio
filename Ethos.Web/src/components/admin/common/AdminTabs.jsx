import React from 'react';
import './AdminTabs.css';

export default function AdminTabs({
  tabs = [], // [{ id, label, count, countTone }]
  activeTab,
  onChange,
  variant = 'pill', // 'pill' or 'underline'
  className = '',
}) {
  return (
    <div className={'admin-tabs-container variant-' + variant + ' ' + className} role="tablist">
      {tabs.map((tab) => {
        const isActive = activeTab === tab.id;
        return (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={isActive}
            className={'admin-tab-item ' + (isActive ? 'active' : '')}
            onClick={() => onChange(tab.id)}
          >
            {tab.icon && <span className="tab-icon">{tab.icon}</span>}
            <span className="tab-label">{tab.label}</span>
            {tab.count !== undefined && tab.count !== null && (
              <span className={'tab-badge-pill ' + (tab.countTone || 'default')}>
                {tab.count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
