import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const githubRepo = "https://github.com/sevensolutions/nomad-iis";

const config: Config = {
  title: 'Nomad IIS',
  tagline: 'Run IIS Workloads on HashiCorp Nomad',
  favicon: 'img/favicon.ico',

  // Set the production url of your site here
  url: 'https://nomad-iis.sevensolutions.cc',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub pages deployment, it is often '/<projectName>/'
  baseUrl: '/',

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'sevensolutions', // Usually your GitHub org/user name.
  projectName: 'nomad-iis', // Usually your repo name.
	trailingSlash: false,

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          // Please change this to your repo.
          // Remove this to remove the "edit this page" links.
          // editUrl: 'https://github.com/facebook/docusaurus/tree/main/packages/create-docusaurus/templates/shared/',
        },
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    // Replace with your project's social card
    image: 'img/docusaurus-social-card.jpg',
    navbar: {
      title: 'Nomad IIS',
      logo: {
        alt: 'Nomad IIS Logo',
        src: 'img/logo.svg',
        srcDark: 'img/logo-dark.svg'
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'gettingStartedSidebar',
          position: 'left',
          label: 'Getting Started',
        },
				{
          type: 'docSidebar',
					sidebarId: 'featuresSidebar',
          position: 'left',
          label: 'Features',
        },
				{
          type: 'docSidebar',
					sidebarId: 'tipsAndTricksSidebar',
          position: 'left',
          label: 'Tips & Tricks',
        },
        {
          href: githubRepo,
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {
              label: 'Getting Started',
              to: '/docs/getting-started',
            },
						{
              label: 'Features',
              to: '/docs/features',
            },
						{
              label: 'Tips & Tricks',
              to: '/docs/tips-and-tricks',
            },
          ],
        },
        {
          title: 'Community',
          items: [
            {
              label: 'Github Discussions',
              href: githubRepo + "/discussions",
            }
          ],
        },
        {
          title: 'More',
          items: [
            {
              label: 'GitHub',
              href: githubRepo,
            },
						{
							label: 'Sponsor',
							href: "https://github.com/sponsors/sevensolutions"
						}
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} sevensolutions. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
			additionalLanguages: ["hcl"]
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
